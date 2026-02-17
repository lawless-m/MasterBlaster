namespace MasterBlaster.Tests.Mbl;

using MasterBlaster.Mbl;
using Xunit;

public class ValidatorTests
{
    private readonly Validator _validator = new();

    [Fact]
    public void Validate_ValidTask_ReturnsNoErrors()
    {
        var task = new TaskDefinition
        {
            Name = "valid_task",
            FileName = "valid_task.mbl",
            Inputs = new List<string> { "customer_name" },
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Fill form",
                    Actions = new List<IAction>
                    {
                        new TypeAction("customer_name", true, "Name Field", false),
                        new ClickAction("Save"),
                        new ExtractAction("result", "Result Field"),
                        new OutputAction("result")
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingExtractForOutput_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Name = "missing_extract",
            FileName = "missing_extract.mbl",
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Output without extract",
                    Actions = new List<IAction>
                    {
                        new OutputAction("result")
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Single(errors);
        Assert.Contains("result", errors[0]);
        Assert.Contains("output", errors[0]);
        Assert.Contains("not been extracted", errors[0]);
    }

    [Fact]
    public void Validate_UndeclaredParameterInTypeAction_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Name = "undeclared_param",
            FileName = "undeclared_param.mbl",
            Inputs = new List<string> { "declared_param" },
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Type with undeclared param",
                    Actions = new List<IAction>
                    {
                        new TypeAction("undeclared_param", true, "Field", false)
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Single(errors);
        Assert.Contains("undeclared_param", errors[0]);
        Assert.Contains("type", errors[0]);
        Assert.Contains("not declared", errors[0]);
    }

    [Fact]
    public void Validate_DeclaredParameterInTypeAction_ReturnsNoError()
    {
        var task = new TaskDefinition
        {
            Name = "declared_param",
            FileName = "declared_param.mbl",
            Inputs = new List<string> { "customer_name" },
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Type with declared param",
                    Actions = new List<IAction>
                    {
                        new TypeAction("customer_name", true, "Name Field", false)
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_UndeclaredParameterInSelectAction_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Name = "undeclared_select",
            FileName = "undeclared_select.mbl",
            Inputs = new List<string> { "other_param" },
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Select with undeclared param",
                    Actions = new List<IAction>
                    {
                        new SelectAction("missing_param", true, "Dropdown")
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Single(errors);
        Assert.Contains("missing_param", errors[0]);
        Assert.Contains("select", errors[0]);
        Assert.Contains("not declared", errors[0]);
    }

    [Fact]
    public void Validate_NoSteps_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Name = "no_steps",
            FileName = "no_steps.mbl",
            Steps = new List<Step>()
        };

        var errors = _validator.Validate(task);

        Assert.Single(errors);
        Assert.Contains("at least one step", errors[0]);
    }

    [Fact]
    public void Validate_NestedIfBlocks_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Name = "nested_if",
            FileName = "nested_if.mbl",
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Step with nested ifs",
                    Actions = new List<IAction>
                    {
                        new IfScreenShowsAction(
                            "Outer condition",
                            new List<IAction>
                            {
                                new IfScreenShowsAction(
                                    "Inner condition",
                                    new List<IAction> { new ClickAction("Nested Button") },
                                    null
                                )
                            },
                            null
                        )
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Single(errors);
        Assert.Contains("Nested", errors[0]);
        Assert.Contains("Inner condition", errors[0]);
    }

    [Fact]
    public void Validate_NestedIfInElseBlock_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Name = "nested_if_else",
            FileName = "nested_if_else.mbl",
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Step with nested if in else",
                    Actions = new List<IAction>
                    {
                        new IfScreenShowsAction(
                            "Outer condition",
                            new List<IAction> { new ClickAction("OK") },
                            new List<IAction>
                            {
                                new IfScreenShowsAction(
                                    "Else nested condition",
                                    new List<IAction> { new ClickAction("Cancel") },
                                    null
                                )
                            }
                        )
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Single(errors);
        Assert.Contains("Nested", errors[0]);
        Assert.Contains("Else nested condition", errors[0]);
    }

    [Fact]
    public void Validate_NonNestedIfBlock_ReturnsNoError()
    {
        var task = new TaskDefinition
        {
            Name = "non_nested_if",
            FileName = "non_nested_if.mbl",
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Step with single if",
                    Actions = new List<IAction>
                    {
                        new IfScreenShowsAction(
                            "Some condition",
                            new List<IAction> { new ClickAction("OK") },
                            new List<IAction> { new ClickAction("Cancel") }
                        )
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ExtractBeforeOutputAcrossSteps_ReturnsNoError()
    {
        var task = new TaskDefinition
        {
            Name = "cross_step_extract",
            FileName = "cross_step.mbl",
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Step 1",
                    Actions = new List<IAction>
                    {
                        new ExtractAction("total", "Total Field")
                    }
                },
                new Step
                {
                    Description = "Step 2",
                    Actions = new List<IAction>
                    {
                        new OutputAction("total")
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_LiteralTypeAction_DoesNotRequireInput()
    {
        var task = new TaskDefinition
        {
            Name = "literal_type",
            FileName = "literal_type.mbl",
            Inputs = new List<string>(),
            Steps = new List<Step>
            {
                new Step
                {
                    Description = "Type literal string",
                    Actions = new List<IAction>
                    {
                        new TypeAction("hello", false, "Field", false)
                    }
                }
            }
        };

        var errors = _validator.Validate(task);

        Assert.Empty(errors);
    }
}
