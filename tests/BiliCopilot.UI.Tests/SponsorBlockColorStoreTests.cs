// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockColorStoreTests
{
    [Fact]
    public void Read_WithoutStoredValue_ReturnsNull()
    {
        var store = CreateStore(out _);

        Assert.Null(store.Read(SponsorBlockCategory.Sponsor));
    }

    [Fact]
    public void WriteThenRead_RoundTripsPerCategory()
    {
        var store = CreateStore(out _);

        store.Write(SponsorBlockCategory.Interaction, 0xFF3366CC);

        Assert.Equal(0xFF3366CCu, store.Read(SponsorBlockCategory.Interaction));
        Assert.Null(store.Read(SponsorBlockCategory.Sponsor));
    }

    [Fact]
    public void Write_PersistsFormattedString()
    {
        var store = CreateStore(out var storage);

        store.Write(SponsorBlockCategory.Intro, 0xFF3366CC);

        Assert.Equal(
            "#FF3366CC",
            storage.Values[SponsorBlockCategoryCatalog.Get(SponsorBlockCategory.Intro).ColorSettingName]);
    }

    [Fact]
    public void Read_CorruptStoredValue_ReturnsNull()
    {
        var store = CreateStore(out var storage);

        storage.Values[SponsorBlockCategoryCatalog.Get(SponsorBlockCategory.Outro).ColorSettingName] = "not-a-color";

        Assert.Null(store.Read(SponsorBlockCategory.Outro));
    }

    [Fact]
    public void Read_StorageThrows_ReturnsNull()
    {
        var store = new SponsorBlockColorStore(new ThrowingStorage());

        Assert.Null(store.Read(SponsorBlockCategory.Preview));
    }

    [Fact]
    public void Reset_RemovesStoredValueAndReturnsToDefault()
    {
        var store = CreateStore(out var storage);
        var key = SponsorBlockCategoryCatalog.Get(SponsorBlockCategory.Filler).ColorSettingName;
        store.Write(SponsorBlockCategory.Filler, 0xFF112233);

        store.Reset(SponsorBlockCategory.Filler);

        Assert.Null(store.Read(SponsorBlockCategory.Filler));
        Assert.False(storage.Values.ContainsKey(key));
    }

    [Fact]
    public void Write_RaisesChangedOnce()
    {
        var store = CreateStore(out _);
        var changes = 0;
        store.Changed += (_, _) => changes++;

        store.Write(SponsorBlockCategory.Sponsor, 0xFF112233);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Reset_RaisesChangedOnce()
    {
        var store = CreateStore(out _);
        var changes = 0;
        store.Changed += (_, _) => changes++;

        store.Reset(SponsorBlockCategory.Sponsor);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Read_DoesNotRaiseChanged()
    {
        var store = CreateStore(out _);
        var changes = 0;
        store.Changed += (_, _) => changes++;

        _ = store.Read(SponsorBlockCategory.Sponsor);

        Assert.Equal(0, changes);
    }

    private static SponsorBlockColorStore CreateStore(out MemoryStorage storage)
    {
        storage = new MemoryStorage();
        return new SponsorBlockColorStore(storage);
    }

    private sealed class MemoryStorage : ISponsorBlockColorStorage
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public string? Read(string key) => Values.TryGetValue(key, out var value) ? value : null;

        public void Write(string key, string value) => Values[key] = value;

        public void Delete(string key) => Values.Remove(key);
    }

    private sealed class ThrowingStorage : ISponsorBlockColorStorage
    {
        public string? Read(string key) => throw new InvalidOperationException("storage unavailable");

        public void Write(string key, string value) => throw new InvalidOperationException("storage unavailable");

        public void Delete(string key) => throw new InvalidOperationException("storage unavailable");
    }
}
