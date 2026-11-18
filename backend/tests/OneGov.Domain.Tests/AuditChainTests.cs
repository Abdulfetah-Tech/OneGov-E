using OneGov.Domain.Audit;

namespace OneGov.Domain.Tests;

public class AuditChainTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static List<AuditEntry> BuildChain(int length)
    {
        var entries = new List<AuditEntry>();
        AuditEntry? previous = null;
        for (var i = 0; i < length; i++)
        {
            previous = AuditChain.CreateNext(previous, Start.AddSeconds(i), $"user-{i}", "citizen.read", $"citizen/{i}", "support request");
            entries.Add(previous);
        }

        return entries;
    }

    [Fact]
    public void A_fresh_chain_verifies()
    {
        var chain = BuildChain(5);

        Assert.True(AuditChain.Verify(chain).IsValid);
        Assert.Equal(AuditChain.GenesisHash, chain[0].PreviousHash);
        Assert.Equal(chain[0].Hash, chain[1].PreviousHash);
    }

    [Fact]
    public void Editing_an_entry_is_detected()
    {
        var chain = BuildChain(4);
        var original = chain[2];
        chain[2] = new AuditEntry(
            original.Sequence, original.OccurredAtUnixMs, "mallory", original.Action,
            original.Subject, original.Purpose, original.PreviousHash, original.Hash);

        var result = AuditChain.Verify(chain);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.FirstBrokenSequence);
    }

    [Fact]
    public void Removing_an_entry_is_detected()
    {
        var chain = BuildChain(4);
        chain.RemoveAt(1);

        var result = AuditChain.Verify(chain);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.FirstBrokenSequence);
    }

    [Fact]
    public void Reordering_entries_is_detected()
    {
        var chain = BuildChain(3);
        (chain[1], chain[2]) = (chain[2], chain[1]);

        Assert.False(AuditChain.Verify(chain).IsValid);
    }

    [Fact]
    public void Hash_is_deterministic_and_sensitive_to_every_field()
    {
        var baseline = AuditChain.ComputeHash(1, 1000, "a", "b", "c", "d", AuditChain.GenesisHash);

        Assert.Equal(baseline, AuditChain.ComputeHash(1, 1000, "a", "b", "c", "d", AuditChain.GenesisHash));
        Assert.NotEqual(baseline, AuditChain.ComputeHash(2, 1000, "a", "b", "c", "d", AuditChain.GenesisHash));
        Assert.NotEqual(baseline, AuditChain.ComputeHash(1, 1001, "a", "b", "c", "d", AuditChain.GenesisHash));
        Assert.NotEqual(baseline, AuditChain.ComputeHash(1, 1000, "x", "b", "c", "d", AuditChain.GenesisHash));
        Assert.NotEqual(baseline, AuditChain.ComputeHash(1, 1000, "a", "b", "c", "e", AuditChain.GenesisHash));
    }

    [Fact]
    public void Field_boundaries_cannot_be_shifted()
    {
        var first = AuditChain.ComputeHash(1, 1, "ab", "c", "s", "p", AuditChain.GenesisHash);
        var second = AuditChain.ComputeHash(1, 1, "a", "bc", "s", "p", AuditChain.GenesisHash);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_window_can_be_verified_with_its_predecessor_hash()
    {
        var chain = BuildChain(6);
        var window = chain.Skip(3).ToList();

        Assert.True(AuditChain.Verify(window, chain[2].Hash, 4).IsValid);
        Assert.False(AuditChain.Verify(window, AuditChain.GenesisHash, 4).IsValid);
    }

    [Fact]
    public void Missing_purpose_is_stored_as_empty_text()
    {
        var entry = AuditChain.CreateNext(null, Start, "system", "startup", "app", null);

        Assert.Equal(string.Empty, entry.Purpose);
        Assert.Equal(Start.ToUnixTimeMilliseconds(), entry.OccurredAtUnixMs);
    }
}
