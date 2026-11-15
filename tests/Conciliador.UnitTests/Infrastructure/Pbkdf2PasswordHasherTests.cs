using Conciliador.Infrastructure.Security;
using FluentAssertions;

namespace Conciliador.UnitTests.Infrastructure;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verifies_the_right_password_and_rejects_the_wrong_one()
    {
        var hash = _hasher.Hash("correct horse");

        _hasher.Verify("correct horse", hash).Should().BeTrue();
        _hasher.Verify("wrong horse", hash).Should().BeFalse();
    }

    [Fact]
    public void Produces_a_different_hash_each_time()
    {
        _hasher.Hash("same").Should().NotBe(_hasher.Hash("same"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("10.@@@.@@@")]
    public void Rejects_malformed_hashes(string hash)
    {
        _hasher.Verify("anything", hash).Should().BeFalse();
    }
}
