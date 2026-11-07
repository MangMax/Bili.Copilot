// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

public class CollaboratorListBuilderTests
{
    [Fact]
    public void Build_WithNullCollaborators_ReturnsEmpty()
        => Assert.Empty(CollaboratorListBuilder.Build(CreateUser("1", "主投稿人"), null));

    [Fact]
    public void Build_WithOnlyPrimary_ReturnsEmpty()
        => Assert.Empty(CollaboratorListBuilder.Build(
            CreateUser("1", "主投稿人"),
            [CreateProfile("1", "主投稿人", "UP主")]));

    [Fact]
    public void Build_ExcludesPrimaryAndKeepsSourceOrder()
    {
        var result = CollaboratorListBuilder.Build(
            CreateUser("1", "主投稿人"),
            [
                CreateProfile("1", "主投稿人", "UP主"),
                CreateProfile("2", "乙", "剪辑"),
                CreateProfile("3", "丙", "参演"),
            ]);

        Assert.Equal(["2", "3"], result.Select(profile => profile.User.Id));
    }

    [Fact]
    public void Build_DeduplicatesRepeatedUsers()
    {
        var result = CollaboratorListBuilder.Build(
            CreateUser("1", "主投稿人"),
            [
                CreateProfile("2", "乙", "剪辑"),
                CreateProfile("2", "乙", "剪辑"),
                CreateProfile("3", "丙", null),
            ]);

        Assert.Equal(["2", "3"], result.Select(profile => profile.User.Id));
    }

    [Fact]
    public void Build_IgnoresCollaboratorsWithoutName()
    {
        var result = CollaboratorListBuilder.Build(
            CreateUser("1", "主投稿人"),
            [
                CreateProfile("2", null, "剪辑"),
                CreateProfile("3", "  ", "参演"),
                CreateProfile("4", "丁", "配音"),
            ]);

        Assert.Single(result);
        Assert.Equal("4", result[0].User.Id);
    }

    [Fact]
    public void Build_PreservesRole()
    {
        var result = CollaboratorListBuilder.Build(
            CreateUser("1", "主投稿人"),
            [CreateProfile("2", "乙", "剪辑")]);

        Assert.Single(result);
        Assert.Equal("剪辑", result[0].Role);
    }

    [Fact]
    public void Build_WithoutPrimary_KeepsAllNamedCollaborators()
    {
        var result = CollaboratorListBuilder.Build(
            null,
            [
                CreateProfile("2", "乙", null),
                CreateProfile("3", "丙", null),
            ]);

        Assert.Equal(["2", "3"], result.Select(profile => profile.User.Id));
    }

    private static UserProfile CreateUser(string id, string? name)
        => new(id, name, null);

    private static PublisherProfile CreateProfile(string id, string? name, string? role)
        => new(CreateUser(id, name), role);
}
