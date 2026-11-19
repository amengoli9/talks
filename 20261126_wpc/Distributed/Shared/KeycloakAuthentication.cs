using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

namespace BabyLog.Distributed;

internal static class KeycloakAuthentication
{
    public static void AddKeycloak(this WebApplicationBuilder builder, string audience)
    {
        var issuer = builder.Configuration["Keycloak:Issuer"] ?? "http://localhost:5182/realms/babylog";
        var metadata = builder.Configuration["Keycloak:MetadataAddress"] ?? $"{issuer}/.well-known/openid-configuration";

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MetadataAddress = metadata;
            options.RequireHttpsMetadata = builder.Configuration.GetValue("Keycloak:RequireHttpsMetadata", true);
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true
            };
        });
        builder.Services.AddAuthorization();
        var keysPath = builder.Configuration["DataProtection:Path"] ?? Path.Combine("data", audience, "keys");
        builder.Services.AddDataProtection().SetApplicationName(audience)
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }
}
