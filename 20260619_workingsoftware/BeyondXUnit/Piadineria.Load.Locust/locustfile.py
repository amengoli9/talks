"""
FITNESS FUNCTION di PERFORMANCE (§5e) — versione Python con Locust.

Stessa idea del test NBomber, altro stack: martelliamo l'endpoint POST /orders
della WebApp esagonale e verifichiamo una SLO (p95 < soglia, zero errori).

Verde di default. Scommenta la riga 🔴 DEMO in OrderService.PlaceOrder
(await Task.Delay), riavvia la WebApp e rilancia: la p95 sfora la soglia e
Locust esce con exit code 1 (in CI la build non passa).

Avvio (headless):
    # 1) in un terminale: avvia la WebApp
    dotnet run --project HexagonalPiadineria/src/HexagonalPiadineria.WebApp \
        --no-launch-profile --urls http://localhost:5099
    # 2) in un altro terminale:
    pip install -r requirements.txt
    locust -f locustfile.py --headless -u 50 -r 50 -t 10s --host http://localhost:5099
"""

import itertools
import random
import sys

from locust import HttpUser, task, between, events

# Console Windows (cp1252) andrebbe in UnicodeEncodeError sulle emoji: forziamo UTF-8.
try:
    sys.stdout.reconfigure(encoding="utf-8")
except (AttributeError, ValueError):
    pass

SLO_P95_MS = 100  # stessa SLO del test NBomber: p95 < 100 ms

# Ogni ordine è identificato dal tavolo (chiave): serve un tavolo diverso a ogni
# richiesta, altrimenti l'inserimento collide sulla chiave già presente.
# La WebApp usa un DB InMemory che vive quanto il processo: partiamo da una base
# casuale così anche lanci ripetuti sulla STESSA istanza non collidono.
# next() su itertools.count è atomico in CPython.
_next_table = itertools.count(random.randint(1, 9) * 100_000_000 + 1)


class PiadinaUser(HttpUser):
    wait_time = between(0, 0.1)

    @task
    def place_order(self):
        payload = {
            "table": next(_next_table),
            "lines": [
                {"piada": "squacquerone e rucola", "price": 5.0, "quantity": 3}
            ],
        }
        with self.client.post("/orders", json=payload, catch_response=True) as resp:
            if resp.status_code != 200:
                resp.failure(f"HTTP {resp.status_code}")


@events.quitting.add_listener
def _check_slo(environment, **_kwargs):
    """A fine run valuta la SLO e imposta l'exit code del processo."""
    stats = environment.stats.total
    p95 = stats.get_response_time_percentile(0.95)
    failures = stats.num_failures

    print()
    print(f"SLO performance:  p95 < {SLO_P95_MS} ms  e  0 errori")
    print(f"Misurato:         p95 = {p95} ms,  errori = {failures}")

    if p95 is None or p95 > SLO_P95_MS or failures > 0:
        print("🔴 ROSSO: SLO violata — performance degradata. In CI questa build NON passa.")
        environment.process_exit_code = 1
    else:
        print("✅ VERDE: la piadineria regge il carico entro la SLO.")
        environment.process_exit_code = 0
