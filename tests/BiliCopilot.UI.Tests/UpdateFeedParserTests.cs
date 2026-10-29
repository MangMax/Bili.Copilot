// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class UpdateFeedParserTests
{
    private const string Atom = """
        <?xml version="1.0" encoding="UTF-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom" xml:lang="en-US">
          <title>Release notes from Bili.Copilot</title>
          <entry>
            <id>tag:github.com,2008:Repository/1/v2.2610.3.0</id>
            <updated>2026-10-28T15:52:37Z</updated>
            <link rel="alternate" type="text/html" href="https://github.com/MangMax/Bili.Copilot/releases/tag/v2.2610.3.0"/>
            <title>Bili.Copilot 2.2610.3.0</title>
            <content type="html">&lt;p&gt;修复 &amp;lt;div&amp;gt; 与 &amp;amp; 的显示&lt;/p&gt;
        &lt;h2&gt;本次更新&lt;/h2&gt;
        &lt;ul&gt;
        &lt;li&gt;&lt;strong&gt;音量记忆&lt;/strong&gt;：调整后落盘&lt;/li&gt;
        &lt;li&gt;字幕优先中文&lt;/li&gt;
        &lt;/ul&gt;</content>
          </entry>
          <entry>
            <id>tag:github.com,2008:Repository/1/v2.2610.2.0</id>
            <updated>2026-10-28T11:23:48Z</updated>
            <link rel="alternate" type="text/html" href="https://github.com/MangMax/Bili.Copilot/releases/tag/v2.2610.2.0"/>
            <title>Bili.Copilot 2.2610.2.0</title>
            <content type="html">&lt;p&gt;旧版本&lt;/p&gt;</content>
          </entry>
        </feed>
        """;

    [Fact]
    public void ParseLatest_TakesFirstEntry()
    {
        var info = UpdateFeedParser.ParseLatest(Atom);

        Assert.NotNull(info);
        Assert.Equal("2.2610.3.0", info!.Version);
        Assert.Equal(2026, info.PublishedAt!.Value.Year);
    }

    [Fact]
    public void ParseLatest_DecodesHtmlEntities()
    {
        var info = UpdateFeedParser.ParseLatest(Atom);

        Assert.Contains("<div>", info!.Notes, StringComparison.Ordinal);
        Assert.Contains("&", info.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseLatest_ConvertsListItemsToBullets()
    {
        var info = UpdateFeedParser.ParseLatest(Atom);

        Assert.Contains("- 音量记忆：调整后落盘", info!.Notes, StringComparison.Ordinal);
        Assert.Contains("本次更新", info.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseLatest_WithEmptyFeed_ReturnsNull()
        => Assert.Null(UpdateFeedParser.ParseLatest("<feed xmlns=\"http://www.w3.org/2005/Atom\"></feed>"));

    [Fact]
    public void ParseLatest_WithMalformedXml_ReturnsNull()
        => Assert.Null(UpdateFeedParser.ParseLatest("not xml"));

    [Fact]
    public void ParseLatest_WithEmptyContent_ReturnsEmptyNotes()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <link rel="alternate" href="https://github.com/o/r/releases/tag/v1.2.3"/>
                <content type="html"></content>
              </entry>
            </feed>
            """;

        var info = UpdateFeedParser.ParseLatest(xml);

        Assert.NotNull(info);
        Assert.Equal("1.2.3", info!.Version);
        Assert.Equal(string.Empty, info.Notes);
    }

    [Fact]
    public void ParseLatest_WithoutPublishedTimestamp_ReturnsNullTimestamp()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <link rel="alternate" href="https://github.com/o/r/releases/tag/v1.2.3"/>
              </entry>
            </feed>
            """;

        Assert.Null(UpdateFeedParser.ParseLatest(xml)!.PublishedAt);
    }
}
