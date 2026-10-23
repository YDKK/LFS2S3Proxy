using LFS2S3Proxy;
using Xunit;

namespace LFS2S3Proxy.Tests
{
    public sealed class TokenValidatorTests
    {
        // URLの先頭セグメントにある正しいトークンを受け入れる。
        [Fact]
        public void MatchesTokenInFirstPathSegment()
        {
            Assert.True(TokenValidator.MatchesUrlToken("/secret/repository/verify", "secret"));
        }

        // 同じ長さでも先頭・中間・末尾の差異を拒否する。
        [Theory]
        [InlineData("/xecret/repository/verify")]
        [InlineData("/seXret/repository/verify")]
        [InlineData("/secreX/repository/verify")]
        public void RejectsSameLengthTokenDifferences(string rawUrl)
        {
            Assert.False(TokenValidator.MatchesUrlToken(rawUrl, "secret"));
        }

        // トークンは大文字小文字を区別して比較する。
        [Fact]
        public void ComparesTokenCaseSensitively()
        {
            Assert.False(TokenValidator.MatchesUrlToken("/Secret/repository/verify", "secret"));
        }

        // 長さ違い、余分な先頭セグメント、区切り欠落を拒否する。
        [Theory]
        [InlineData("/secre/repository/verify")]
        [InlineData("/secrets/repository/verify")]
        [InlineData("/prefix/secret/repository/verify")]
        [InlineData("/secret")]
        [InlineData("/secret?query=value")]
        [InlineData("secret/repository/verify")]
        public void RejectsMalformedOrDifferentTokenSegments(string rawUrl)
        {
            Assert.False(TokenValidator.MatchesUrlToken(rawUrl, "secret"));
        }

        // 未設定の期待トークンは認証に使わない。
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void RejectsNullOrEmptyExpectedToken(string? expectedToken)
        {
            Assert.False(TokenValidator.MatchesUrlToken("/secret/repository/verify", expectedToken!));
        }

        // UnicodeトークンをUTF-16コード単位のまま比較する。
        [Fact]
        public void MatchesUnicodeTokenWithoutNormalization()
        {
            Assert.True(TokenValidator.MatchesUrlToken("/秘密🔑/repository/verify", "秘密🔑"));
            Assert.False(TokenValidator.MatchesUrlToken("/e\u0301/repository/verify", "\u00E9"));
            Assert.False(TokenValidator.MatchesUrlToken("/%C3%A9/repository/verify", "\u00E9"));
        }

        // 不正サロゲートを含む文字列もUTF-16コード単位の差を保持する。
        [Fact]
        public void DistinguishesUnpairedSurrogates()
        {
            Assert.True(TokenValidator.MatchesUrlToken("/\uD800/repository/verify", "\uD800"));
            Assert.False(TokenValidator.MatchesUrlToken("/\uD801/repository/verify", "\uD800"));
        }

        // batchとverifyのURLでトークン位置を同じように検証する。
        [Theory]
        [InlineData("/secret/repository/objects/batch")]
        [InlineData("/secret/repository/verify")]
        public void MatchesTokenForBatchAndVerifyRoutes(string rawUrl)
        {
            Assert.True(TokenValidator.MatchesUrlToken(rawUrl, "secret"));
        }
    }
}
