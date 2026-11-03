// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Prompts;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 提示词构建：按类型把标题、描述、字幕、评论、正文与问题填入模板。
/// </summary>
public class AIPromptComposerTests
{
    [Fact]
    public void Build_VideoSummary_FillsTitleDescriptionAndSubtitle()
    {
        var variables = new AIPromptVariables
        {
            Title = "视频标题",
            Description = "视频简介",
            Subtitle = "00:01 - 00:02: 开场白",
        };

        var prompt = AIPromptComposer.Build(AIPromptKind.VideoSummary, "{title}//{description}//{subtitle}", variables);

        Assert.Equal("视频标题//视频简介//00:01 - 00:02: 开场白", prompt);
    }

    [Fact]
    public void Build_VideoSummary_MissingSubtitleUsesDefaultText()
    {
        var prompt = AIPromptComposer.Build(AIPromptKind.VideoSummary, "{title} 的总结：{subtitle}", new AIPromptVariables { Title = "视频标题" });

        Assert.Equal("视频标题 的总结：无字幕", prompt);
    }

    [Fact]
    public void Build_VideoEvaluation_FillsTitleDescriptionSubtitleAndComments()
    {
        var variables = new AIPromptVariables
        {
            Title = "视频标题",
            Description = "视频简介",
            Subtitle = "字幕内容",
            Comments = "评论内容",
        };

        var prompt = AIPromptComposer.Build(
            AIPromptKind.VideoEvaluation,
            "{title}//{description}//{subtitle}//{comments}",
            variables);

        Assert.Equal("视频标题//视频简介//字幕内容//评论内容", prompt);
    }

    [Fact]
    public void Build_VideoEvaluation_MissingCommentsUsesDefaultText()
    {
        var prompt = AIPromptComposer.Build(
            AIPromptKind.VideoEvaluation,
            "热门评论：{comments}",
            new AIPromptVariables { Comments = string.Empty });

        Assert.Equal("热门评论：无评论", prompt);
    }

    [Fact]
    public void Build_ArticleSummary_FillsTitleAndArticleContent()
    {
        var variables = new AIPromptVariables
        {
            Title = "文章标题",
            ArticleContent = "正文内容",
        };

        var prompt = AIPromptComposer.Build(AIPromptKind.ArticleSummary, "{title}//{content}", variables);

        Assert.Equal("文章标题//正文内容", prompt);
    }

    [Fact]
    public void Build_ArticleEvaluation_FillsTitleArticleContentAndComments()
    {
        var variables = new AIPromptVariables
        {
            Title = "文章标题",
            ArticleContent = "正文内容",
            Comments = "评论内容",
        };

        var prompt = AIPromptComposer.Build(AIPromptKind.ArticleEvaluation, "{title}//{content}//{comments}", variables);

        Assert.Equal("文章标题//正文内容//评论内容", prompt);
    }

    [Fact]
    public void Build_ArticleEvaluation_MissingCommentsUsesDefaultText()
    {
        var prompt = AIPromptComposer.Build(
            AIPromptKind.ArticleEvaluation,
            "{content}//{comments}",
            new AIPromptVariables { ArticleContent = "正文内容", Comments = null });

        Assert.Equal("正文内容//无评论", prompt);
    }

    [Fact]
    public void Build_VariableValuesContainingPlaceholderLiteralsAreNotReplacedAgain()
    {
        var variables = new AIPromptVariables
        {
            Title = "标题里写了 {subtitle} 与 {comments}",
            Subtitle = "字幕里提到 {title}",
            Comments = "评论里提到 {content}",
            ArticleContent = "正文",
        };

        var prompt = AIPromptComposer.Build(
            AIPromptKind.VideoEvaluation,
            "{title}//{subtitle}//{comments}",
            variables);

        Assert.Equal("标题里写了 {subtitle} 与 {comments}//字幕里提到 {title}//评论里提到 {content}", prompt);
    }

    [Fact]
    public void Build_CustomTemplateWithMissingPlaceholdersKeepsTheRestIntact()
    {
        var prompt = AIPromptComposer.Build(
            AIPromptKind.VideoSummary,
            "只有标题：{title}",
            new AIPromptVariables { Title = "视频标题", Subtitle = "字幕内容" });

        Assert.Equal("只有标题：视频标题", prompt);
    }

    [Fact]
    public void Build_VideoSummary_TokensOfOtherKindsAreKeptAsIs()
    {
        var variables = new AIPromptVariables
        {
            Title = "视频标题",
            Subtitle = "字幕内容",
            Comments = "评论内容",
            ArticleContent = "正文内容",
            Question = "问题",
        };

        var prompt = AIPromptComposer.Build(
            AIPromptKind.VideoSummary,
            "{title}|{subtitle}|{comments}|{content}|{question}",
            variables);

        Assert.Equal("视频标题|字幕内容|{comments}|{content}|{question}", prompt);
    }

    [Fact]
    public void Build_ArticleSummary_TokensOfOtherKindsAreKeptAsIs()
    {
        var variables = new AIPromptVariables
        {
            Title = "文章标题",
            ArticleContent = "正文内容",
            Description = "简介",
            Subtitle = "字幕内容",
            Comments = "评论内容",
        };

        var prompt = AIPromptComposer.Build(
            AIPromptKind.ArticleSummary,
            "{title}|{content}|{description}|{subtitle}|{comments}",
            variables);

        Assert.Equal("文章标题|正文内容|{description}|{subtitle}|{comments}", prompt);
    }

    [Fact]
    public void Build_UnknownPlaceholdersAreKeptAsIs()
    {
        var prompt = AIPromptComposer.Build(
            AIPromptKind.VideoSummary,
            "{title}|{unknown}|{foo_bar}|{TITLE}|{未闭合",
            new AIPromptVariables { Title = "视频标题" });

        Assert.Equal("视频标题|{unknown}|{foo_bar}|{TITLE}|{未闭合", prompt);
    }

    [Fact]
    public void Build_LargeTemplateKeepsStructureAndFillsEveryOccurrence()
    {
        const string template = """
            以下是内容：

            ------------------------------
            视频标题：{title}
            ---
            视频简介：
            {description}
            ---
            视频字幕：
            {subtitle}
            ---
            热门评论：
            {comments}
            ------------------------------

            再次提醒：{title}
            """;

        var variables = new AIPromptVariables
        {
            Title = "标题",
            Description = "简介",
            Subtitle = "字幕",
            Comments = "评论",
        };

        var prompt = AIPromptComposer.Build(AIPromptKind.VideoEvaluation, template, variables);

        var expected = """
            以下是内容：

            ------------------------------
            视频标题：标题
            ---
            视频简介：
            简介
            ---
            视频字幕：
            字幕
            ---
            热门评论：
            评论
            ------------------------------

            再次提醒：标题
            """;
        Assert.Equal(expected, prompt);
    }

    [Fact]
    public void Build_VideoQuestion_FillsTitleDescriptionSubtitleAndQuestion()
    {
        var variables = new AIPromptVariables
        {
            Title = "视频标题",
            Description = "视频简介",
            Subtitle = "字幕内容",
            Question = "讲了什么？",
        };

        var prompt = AIPromptComposer.Build(
            AIPromptKind.VideoQuestion,
            "{title}//{description}//{subtitle}//{question}",
            variables);

        Assert.Equal("视频标题//视频简介//字幕内容//讲了什么？", prompt);
    }

    [Fact]
    public void Build_ArticleQuestion_FillsTitleArticleContentAndQuestion()
    {
        var variables = new AIPromptVariables
        {
            Title = "文章标题",
            ArticleContent = "正文内容",
            Question = "结论是什么？",
        };

        var prompt = AIPromptComposer.Build(
            AIPromptKind.ArticleQuestion,
            "{title}//{content}//{question}",
            variables);

        Assert.Equal("文章标题//正文内容//结论是什么？", prompt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_QuestionKindWithoutQuestionThrows(string? question)
    {
        var variables = new AIPromptVariables { Title = "视频标题", Question = question };

        Assert.Throws<ArgumentException>(
            () => AIPromptComposer.Build(AIPromptKind.VideoQuestion, "{title}//{question}", variables));
        Assert.Throws<ArgumentException>(
            () => AIPromptComposer.Build(AIPromptKind.ArticleQuestion, "{title}//{question}", variables));
    }

    [Fact]
    public void Build_QuestionKindWithoutVariablesThrows()
    {
        Assert.Throws<ArgumentException>(
            () => AIPromptComposer.Build(AIPromptKind.ArticleQuestion, "{question}", null));
    }

    [Fact]
    public void Build_NonQuestionKindIgnoresQuestion()
    {
        var variables = new AIPromptVariables { Title = "视频标题", Question = "讲了什么？" };

        var prompt = AIPromptComposer.Build(AIPromptKind.VideoSummary, "{title}//{question}", variables);

        Assert.Equal("视频标题//{question}", prompt);
    }

    [Fact]
    public void Build_QuestionKindWithoutQuestionPlaceholderStillRequiresQuestion()
    {
        Assert.Throws<ArgumentException>(
            () => AIPromptComposer.Build(AIPromptKind.VideoQuestion, "{title}", new AIPromptVariables { Title = "视频标题" }));
    }
}
