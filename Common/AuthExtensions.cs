using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    public static class AuthExtensions
    {
        public static IServiceCollection AddKeyCloakAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication()
                .AddKeycloakJwtBearer(serviceName: "keycloak", realm: "OverflowApp", options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.Audience = "overflow";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuers = [
                            "http://localhost:6001/realms/OverflowApp",
                            "http://keycloak:8080/realms/OverflowApp",
                            "http://id.overflow.local/realms/OverflowApp",
                            "https://id.overflow.local/realms/OverflowApp"
                        ],
                        ClockSkew = TimeSpan.Zero 
                    };
                });

            return services;
        }
    }
}
