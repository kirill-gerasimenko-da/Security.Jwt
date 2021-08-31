using Bogus;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NetDevPack.Security.Jwt.DefaultStore.Memory;
using NetDevPack.Security.Jwt.Interfaces;
using NetDevPack.Security.Jwt.Jwk;
using NetDevPack.Security.Jwt.Jwks;
using NetDevPack.Security.Jwt.Model;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Xunit;

namespace NetDevPack.Security.Jwt.Tests.Jwks
{
    public class JsonWebKeySetServiceTests
    {
        private readonly JwksService _jwksService;
        private readonly IJsonWebKeyStore _store;
        private readonly Mock<IOptions<JwksOptions>> _options;

        public JsonWebKeySetServiceTests()
        {
            _options = new Mock<IOptions<JwksOptions>>();
            _store = new InMemoryStore(_options.Object);
            _jwksService = new JwksService(_store, new JwkService(), _options.Object);
            _options.Setup(s => s.Value).Returns(new JwksOptions());
        }

        [Fact]
        public void ShouldGenerateDefaultSigning()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            var sign = _jwksService.GenerateSigningCredentials();
            var current = _jwksService.GetCurrentSigningCredentials();
            current.Kid.Should().Be(sign.Kid);
        }

        [Fact]
        public void ShouldNotThrowExceptionWhenGetSignManyTimes()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            var currentA = _jwksService.GetCurrentSigningCredentials();
            var currentB = _jwksService.GetCurrentSigningCredentials();
            var currentCg = _jwksService.GetCurrentSigningCredentials();

            var token = new SecurityTokenDescriptor()
            {
                Issuer = "test.jwt",
                Subject = new ClaimsIdentity(),
                Expires = DateTime.UtcNow.AddMinutes(3),
                SigningCredentials = _jwksService.GetCurrentSigningCredentials()
            };
        }

        [Fact]
        public void ShouldGenerateFiveDefaultSigning()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            _store.Clear();
            var keysGenerated = new List<SigningCredentials>();
            for (int i = 0; i < 5; i++)
            {
                var sign = _jwksService.GenerateSigningCredentials();
                keysGenerated.Add(sign);
            }

            var current = _jwksService.GetLastKeysCredentials(JsonWebKeyType.Jws, 5);
            foreach (var securityKey in current)
            {
                keysGenerated.Should().Contain(s => s.Kid == securityKey.KeyId);
            }
        }

        [Fact]
        public void ShouldGenerateFiveDefaultEncrypting()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            _store.Clear();
            var keysGenerated = new List<EncryptingCredentials>();
            for (int i = 0; i < 5; i++)
            {
                var sign = _jwksService.GenerateEncryptingCredentials();
                keysGenerated.Add(sign);
            }

            var current = _jwksService.GetLastKeysCredentials(JsonWebKeyType.Jwe, 5);
            foreach (var securityKey in current)
            {
                keysGenerated.Should().Contain(s => s.Key.KeyId == securityKey.KeyId);
            }
        }

        [Fact]
        public void ShouldGenerateRsa()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_", Jws = JwsAlgorithm.RS512 });
            _store.Clear();
            var sign = _jwksService.GenerateSigningCredentials();
            sign.Algorithm.Should().Be(JwsAlgorithm.RS512);
        }


        [Fact]
        public void ShouldGenerateECDsa()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { Jws = JwsAlgorithm.ES256, KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            var sign = _jwksService.GenerateSigningCredentials();
            var current = _store.GetCurrentKey(JsonWebKeyType.Jws);
            current.KeyId.Should().Be(sign.Kid);
            current.JwsAlgorithm.Should().Be(SecurityAlgorithms.EcdsaSha256);
        }

        [Fact]
        public void ShouldValidateJweAndJws()
        {
            var options = new JwksOptions()
            {
                KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_",
            };

            var encryptingCredentials = _jwksService.GenerateEncryptingCredentials(options);
            var signingCredentials = _jwksService.GenerateSigningCredentials(options);

            var handler = new JsonWebTokenHandler();
            var now = DateTime.Now;
            var jwtE = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                EncryptingCredentials = encryptingCredentials
            };
            var jwtS = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                SigningCredentials = signingCredentials
            };


            var jwe = handler.CreateToken(jwtE);
            var jws = handler.CreateToken(jwtS);

            var jweResult = handler.ValidateToken(jwe,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    RequireSignedTokens = false,
                    TokenDecryptionKey = encryptingCredentials.Key
                });
            var jwsResult = handler.ValidateToken(jws,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    RequireSignedTokens = false,
                    TokenDecryptionKey = encryptingCredentials.Key
                });

            jweResult.IsValid.Should().BeTrue();
        }



        public Faker<Claim> GenerateClaim()
        {
            return new Faker<Claim>().CustomInstantiator(f => new Claim(f.Internet.DomainName(), f.Lorem.Text()));
        }
    }
}
