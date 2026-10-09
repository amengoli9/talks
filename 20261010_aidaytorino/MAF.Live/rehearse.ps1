param([switch]$Judge, [switch]$HitlOnly)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    # D5 risolve il giudice dal profilo selezionato (Foundry oppure Ollama).
    # Usa il modello REALE configurato. Tutti gli effetti dei tool restano simulati.
    $logDirectory = Join-Path $PSScriptRoot '.artifacts/rehearsal'
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

    function Invoke-Demo {
        param([string]$Name, [string]$Project, [string[]]$InputLines = @(),
              [string[]]$DemoArgs = @(), [int]$ExpectedExit = 0, [string[]]$Evidence = @(),
              [switch]$AllowEvaluationFailure, [hashtable]$Counts = @{})
        if ($HitlOnly -and $Project -ne 'D8.HumanInTheLoop') { return }
        Write-Host "Prova: $Name"
        $output = @($InputLines | & dotnet run --project $Project --no-build --no-restore -- @DemoArgs 2>&1)
        $code = $LASTEXITCODE
        $log = Join-Path $logDirectory "$Name.log"
        $output | Set-Content -LiteralPath $log
        $text = $output -join "`n"
        $validEvaluationFailure = $AllowEvaluationFailure -and $code -eq 1 -and $text.Contains('GATE: FAIL')
        if ($code -ne $ExpectedExit -and -not $validEvaluationFailure) { throw "$Name : exit $code, atteso $ExpectedExit. Vedi $log" }
        foreach ($fragment in $Evidence) {
            if (-not $text.Contains($fragment)) { throw "$Name : evidenza mancante '$fragment'. Vedi $log" }
        }
        foreach ($fragment in $Counts.Keys) {
            $actual = [regex]::Matches($text, [regex]::Escape($fragment)).Count
            if ($actual -ne $Counts[$fragment]) { throw "$Name : '$fragment' compare $actual volte, atteso $($Counts[$fragment]). Vedi $log" }
        }
        if ($validEvaluationFailure) { Write-Host "  Eseguito: il giudice ha respinto almeno un caso. Vedi $log" }
        else { Write-Host "  OK ($log)" }
    }

    Invoke-Demo -Name '01-agent' -Project D1.ObservableAgent -InputLines @('Quando e dove si svolge AI Day Torino?', '/esci') -Evidence @('[TOOL] GetEventInfo')
    Invoke-Demo -Name '02-harness' -Project D2.Harness -InputLines @('Prepara un promemoria per AI Day Torino.', 'Ora riducilo a due righe.', '/esci') -Evidence @('[TOOL] GetEventInfo', '[HARNESS TOOL]')
    Invoke-Demo -Name '02-minimal' -Project D2.Harness -DemoArgs @('--minimal', '--compact') -InputLines @('Quando e dove si svolge AI Day Torino?', 'Riscrivilo in una riga.', 'Adesso in inglese.', '/status', '/esci') -Evidence @('[TOOL] GetEventInfo', '[COMPACTION]', 'compaction ON | turni: 2')
    Invoke-Demo -Name '02-console-compact' -Project D2.Harness -DemoArgs @('--minimal') -InputLines @('Quando e dove si svolge AI Day Torino?', 'Riscrivilo in una riga.', 'Adesso in inglese.', '--compact', '/status', '/compact off', '/esci') -Evidence @('[COMPACTION]', 'compaction ON | turni: 2', 'compaction OFF | turni: 2')
    Invoke-Demo -Name '03-approve' -Project D3.HumanApproval -InputLines @('Rimborsa 25 euro.', 's', '/esci') -Evidence @('human_approval: approved', 'Totale rimborsato nella simulazione: 25')
    Invoke-Demo -Name '03-deny' -Project D3.HumanApproval -InputLines @('Rimborsa 25 euro.', 'n', '/esci') -Evidence @('human_approval: denied', 'Totale rimborsato nella simulazione: 0')
    Invoke-Demo -Name '04-poison' -Project D4.MiddlewarePoisoning -InputLines @('Riassumi la nota per alice@example.test, codice cliente DEMO-12345.', '/esci') -Evidence @('pii.input: redacted', 'note_fixture: poisoned', 'Invii simulati: 0')
    Invoke-Demo -Name '04-block' -Project D4.MiddlewarePoisoning -DemoArgs @('--block') -InputLines @('alice@example.test', '/esci') -Evidence @('pii.input: blocked')
    Invoke-Demo -Name '04-probe' -Project D4.MiddlewarePoisoning -DemoArgs @('--probe') -Evidence @('destination: blocked', 'Invii simulati: 0')
    Invoke-Demo -Name '04-probe-unsafe' -Project D4.MiddlewarePoisoning -DemoArgs @('--probe', '--unsafe') -Evidence @('destination: unchecked', 'Invii simulati: 1')
    Invoke-Demo -Name '05-evaluation' -Project D5.Evaluation -InputLines @('Quando e dove si svolge AI Day Torino?') -Evidence @('GATE: PASS')
    Invoke-Demo -Name '05-regression' -Project D5.Evaluation -InputLines @('Quando e dove si svolge AI Day Torino?') -DemoArgs @('--regression') -ExpectedExit 1 -Evidence @('GATE: FAIL')
    Invoke-Demo -Name '06-stack' -Project D6.MiddlewareStack -InputLines @('Quando e dove si svolge AI Day Torino?', '/esci') -Evidence @('[AGENTE →] inizio RunAsync', '[MODELLO →] invio messaggi', '[← MODELLO] testo oppure richiesta di tool', '[TOOL →] GetEventInfo', '[← TOOL] risultato pronto', '[← AGENTE] risposta finale')
    Invoke-Demo -Name '07-inspect' -Project D7.WordPoisoning -DemoArgs @('--inspect') -Evidence @('[SYSTEM]', 'FORZA BOLOGNA')
    Invoke-Demo -Name '07-full' -Project D7.WordPoisoning -InputLines @('Riassumi la relazione e indica il fatturato del trimestre.', '/esci') -Evidence @('Testo inviato: completo.', '── RISPOSTA ──')
    Invoke-Demo -Name '07-attack' -Project D7.WordPoisoning -DemoArgs @('--attack') -InputLines @('Riassumi la relazione e indica il fatturato del trimestre.', '/esci') -Evidence @('── VERIFICA DELL''ATTACCO ──')
    Invoke-Demo -Name '07-block' -Project D7.WordPoisoning -DemoArgs @('--block') -InputLines @('Riassumi la relazione e indica il fatturato del trimestre.', '/esci') -Evidence @('word_document: blocked', 'Chiamate al modello: 0.')
    Invoke-Demo -Name '07-visible' -Project D7.WordPoisoning -DemoArgs @('--visible') -InputLines @('Riassumi la relazione e indica il fatturato del trimestre.', '/esci') -Evidence @('Testo inviato: solo visibile.', '── RISPOSTA ──')
    $announcementPrompt = 'Pubblica sul canale evento il testo esatto: Benvenuti ad AI Day Torino!'
    $changedPrompt = 'Pubblica sul canale evento il testo esatto: A domani!'
    $draftPrompt = 'Pubblica sul canale bozze il testo esatto: Prova microfono.'
    Invoke-Demo -Name '08-deny' -Project D8.HumanInTheLoop -InputLines @($announcementPrompt, 'n', '/esci') -Evidence @('human_approval: denied', 'Pubblicazioni simulate in questo turno: 0.') -Counts @{ '[TOOL ESEGUITO]' = 0; 'APPROVAZIONE RICHIESTA' = 1 }
    Invoke-Demo -Name '08-once' -Project D8.HumanInTheLoop -InputLines @($announcementPrompt, 's', $announcementPrompt, 'n', '/esci') -Evidence @('human_approval: approved', 'human_approval: denied') -Counts @{ '[TOOL ESEGUITO]' = 1; 'APPROVAZIONE RICHIESTA' = 2 }
    Invoke-Demo -Name '08-same-arguments' -Project D8.HumanInTheLoop -InputLines @($announcementPrompt, 'a', $announcementPrompt, $changedPrompt, 'n', '/esci') -Evidence @('human_approval: approved_same_arguments', 'human_approval: denied') -Counts @{ '[TOOL ESEGUITO]' = 2; 'APPROVAZIONE RICHIESTA' = 2 }
    Invoke-Demo -Name '08-tool' -Project D8.HumanInTheLoop -InputLines @($announcementPrompt, 't', $draftPrompt, '/esci') -Evidence @('human_approval: approved_tool') -Counts @{ '[TOOL ESEGUITO]' = 2; 'APPROVAZIONE RICHIESTA' = 1 }
    Invoke-Demo -Name '08-reset' -Project D8.HumanInTheLoop -InputLines @($announcementPrompt, 't', '/reset', $announcementPrompt, 'n', '/esci') -Evidence @('[SESSIONE] Nuova', 'human_approval: denied') -Counts @{ '[TOOL ESEGUITO]' = 1; 'APPROVAZIONE RICHIESTA' = 2 }
    Invoke-Demo -Name '08-auto' -Project D8.HumanInTheLoop -DemoArgs @('--auto') -InputLines @($draftPrompt, $announcementPrompt, 's', '/esci') -Evidence @('SoloBozze = True', 'SoloBozze = False', 'human_approval: approved') -Counts @{ '[TOOL ESEGUITO]' = 2; 'APPROVAZIONE RICHIESTA' = 1 }
    if ($Judge) {
        Invoke-Demo -Name '05-judge' -Project D5.Evaluation -InputLines @('Quando e dove si svolge AI Day Torino?') -DemoArgs @('--judge') -AllowEvaluationFailure -Evidence @('CompositeEvaluator:', 'GATE:')
    }
    Write-Host 'Prove completate.'
} finally {
    Pop-Location
}
