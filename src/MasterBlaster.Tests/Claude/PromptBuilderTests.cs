namespace MasterBlaster.Tests.Claude;

using MasterBlaster.Claude;
using Xunit;

public class PromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_ContainsResolution()
    {
        var prompt = PromptBuilder.BuildSystemPrompt(1920, 1080);

        Assert.Contains("1920", prompt);
        Assert.Contains("1080", prompt);
        Assert.Contains("1920x1080", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsAutomationContext()
    {
        var prompt = PromptBuilder.BuildSystemPrompt(1024, 768);

        Assert.Contains("automation", prompt);
        Assert.Contains("screenshot", prompt.ToLowerInvariant());
    }

    [Fact]
    public void BuildExpectPrompt_ContainsDescription()
    {
        var prompt = PromptBuilder.BuildExpectPrompt("Main screen is visible");

        Assert.Contains("Main screen is visible", prompt);
        Assert.Contains("MATCH", prompt);
        Assert.Contains("NO_MATCH", prompt);
        Assert.Contains("UNCERTAIN", prompt);
    }

    [Fact]
    public void BuildClickPrompt_ContainsTarget()
    {
        var prompt = PromptBuilder.BuildClickPrompt("Save Button");

        Assert.Contains("Save Button", prompt);
        Assert.Contains("x,y", prompt);
        Assert.Contains("NOT_FOUND", prompt);
    }

    [Fact]
    public void BuildTypeFieldPrompt_ContainsTarget()
    {
        var prompt = PromptBuilder.BuildTypeFieldPrompt("Customer Name");

        Assert.Contains("Customer Name", prompt);
        Assert.Contains("x,y", prompt);
        Assert.Contains("NOT_FOUND", prompt);
    }

    [Fact]
    public void BuildSelectDropdownPrompt_ContainsTarget()
    {
        var prompt = PromptBuilder.BuildSelectDropdownPrompt("Payment Terms");

        Assert.Contains("Payment Terms", prompt);
        Assert.Contains("dropdown", prompt.ToLowerInvariant());
        Assert.Contains("x,y", prompt);
        Assert.Contains("NOT_FOUND", prompt);
    }

    [Fact]
    public void BuildSelectOptionPrompt_ContainsValue()
    {
        var prompt = PromptBuilder.BuildSelectOptionPrompt("Net 30");

        Assert.Contains("Net 30", prompt);
        Assert.Contains("NOT_FOUND", prompt);
    }

    [Fact]
    public void BuildExtractPrompt_ContainsSource()
    {
        var prompt = PromptBuilder.BuildExtractPrompt("Total Amount");

        Assert.Contains("Total Amount", prompt);
        Assert.Contains("EMPTY", prompt);
        Assert.Contains("NOT_FOUND", prompt);
    }

    [Fact]
    public void BuildIfScreenShowsPrompt_ContainsCondition()
    {
        var prompt = PromptBuilder.BuildIfScreenShowsPrompt("Confirmation dialog");

        Assert.Contains("Confirmation dialog", prompt);
        Assert.Contains("YES", prompt);
        Assert.Contains("NO", prompt);
    }

    [Fact]
    public void BuildClickPrompt_SpecialCharactersInTarget_ArePreserved()
    {
        var prompt = PromptBuilder.BuildClickPrompt("File > Export > PDF");

        Assert.Contains("File > Export > PDF", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_DifferentResolutions_ReflectedInPrompt()
    {
        var prompt800x600 = PromptBuilder.BuildSystemPrompt(800, 600);
        var prompt1920x1080 = PromptBuilder.BuildSystemPrompt(1920, 1080);

        Assert.Contains("800x600", prompt800x600);
        Assert.Contains("1920x1080", prompt1920x1080);
        Assert.DoesNotContain("1920", prompt800x600);
        Assert.DoesNotContain("800", prompt1920x1080);
    }
}
