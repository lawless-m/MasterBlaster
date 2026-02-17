namespace MasterBlaster.Tests.Mbl;

using MasterBlaster.Mbl;
using Xunit;

public class ParserTests
{
    private readonly Lexer _lexer = new();
    private readonly Parser _parser = new();

    private TaskDefinition ParseSource(string source, string fileName = "test_task")
    {
        var tokens = _lexer.Tokenize(source);
        return _parser.Parse(tokens, fileName);
    }

    [Fact]
    public void Parse_MinimalTask_ReturnsTaskWithOneStep()
    {
        var source = """
            task "Login"
            step "Click login button"
                click "Login"
            """;

        var task = ParseSource(source);

        Assert.Equal("Login", task.Name);
        Assert.Equal("test_task", task.FileName);
        Assert.Empty(task.Inputs);
        Assert.Single(task.Steps);
        Assert.Equal("Click login button", task.Steps[0].Description);
        Assert.Single(task.Steps[0].Actions);
        Assert.IsType<ClickAction>(task.Steps[0].Actions[0]);
    }

    [Fact]
    public void Parse_InputDeclaration_ParsesMultipleParameters()
    {
        var source = """
            task "Create Invoice"
            input customer_name, invoice_number, amount
            step "Fill form"
                click "New"
            """;

        var task = ParseSource(source);

        Assert.Equal(3, task.Inputs.Count);
        Assert.Equal("customer_name", task.Inputs[0]);
        Assert.Equal("invoice_number", task.Inputs[1]);
        Assert.Equal("amount", task.Inputs[2]);
    }

    [Fact]
    public void Parse_AllActionTypes_RecognizesEachAction()
    {
        var source = """
            task "All Actions"
            step "Test all actions"
                expect "Main screen is visible"
                click "OK Button"
                double-click "File Item"
                right-click "Context Target"
                type "hello" into "Text Field"
                select "Option A" in "Dropdown"
                key Ctrl+S
                extract total from "Total Field"
                output total
                screenshot
                abort "Something went wrong"
            """;

        var task = ParseSource(source);
        var actions = task.Steps[0].Actions;

        Assert.Equal(11, actions.Count);
        Assert.IsType<ExpectAction>(actions[0]);
        Assert.Equal("Main screen is visible", ((ExpectAction)actions[0]).Description);

        Assert.IsType<ClickAction>(actions[1]);
        Assert.Equal("OK Button", ((ClickAction)actions[1]).Target);

        Assert.IsType<DoubleClickAction>(actions[2]);
        Assert.Equal("File Item", ((DoubleClickAction)actions[2]).Target);

        Assert.IsType<RightClickAction>(actions[3]);
        Assert.Equal("Context Target", ((RightClickAction)actions[3]).Target);

        Assert.IsType<TypeAction>(actions[4]);
        var typeAction = (TypeAction)actions[4];
        Assert.Equal("hello", typeAction.Value);
        Assert.False(typeAction.IsParam);
        Assert.Equal("Text Field", typeAction.Target);
        Assert.False(typeAction.Append);

        Assert.IsType<SelectAction>(actions[5]);
        var selectAction = (SelectAction)actions[5];
        Assert.Equal("Option A", selectAction.Value);
        Assert.False(selectAction.IsParam);
        Assert.Equal("Dropdown", selectAction.Target);

        Assert.IsType<KeyAction>(actions[6]);
        Assert.Equal("Ctrl+S", ((KeyAction)actions[6]).KeyCombo);

        Assert.IsType<ExtractAction>(actions[7]);
        var extractAction = (ExtractAction)actions[7];
        Assert.Equal("total", extractAction.VariableName);
        Assert.Equal("Total Field", extractAction.Source);

        Assert.IsType<OutputAction>(actions[8]);
        Assert.Equal("total", ((OutputAction)actions[8]).VariableName);

        Assert.IsType<ScreenshotAction>(actions[9]);

        Assert.IsType<AbortAction>(actions[10]);
        Assert.Equal("Something went wrong", ((AbortAction)actions[10]).Message);
    }

    [Fact]
    public void Parse_IfElseEnd_CreatesIfScreenShowsAction()
    {
        var source = """
            task "Conditional"
            step "Handle dialog"
                if screen shows "Save dialog"
                    click "Save"
                else
                    click "Cancel"
                end
            """;

        var task = ParseSource(source);
        var actions = task.Steps[0].Actions;

        Assert.Single(actions);
        var ifAction = Assert.IsType<IfScreenShowsAction>(actions[0]);
        Assert.Equal("Save dialog", ifAction.Condition);

        Assert.Single(ifAction.Then);
        Assert.IsType<ClickAction>(ifAction.Then[0]);
        Assert.Equal("Save", ((ClickAction)ifAction.Then[0]).Target);

        Assert.NotNull(ifAction.Else);
        Assert.Single(ifAction.Else);
        Assert.IsType<ClickAction>(ifAction.Else[0]);
        Assert.Equal("Cancel", ((ClickAction)ifAction.Else[0]).Target);
    }

    [Fact]
    public void Parse_IfWithoutElse_HasNullElseBlock()
    {
        var source = """
            task "Conditional"
            step "Handle dialog"
                if screen shows "Warning"
                    click "OK"
                end
            """;

        var task = ParseSource(source);
        var ifAction = Assert.IsType<IfScreenShowsAction>(task.Steps[0].Actions[0]);

        Assert.Equal("Warning", ifAction.Condition);
        Assert.Single(ifAction.Then);
        Assert.Null(ifAction.Else);
    }

    [Fact]
    public void Parse_OnTimeoutHandler_ParsesCorrectly()
    {
        var source = """
            task "With Timeout Handler"
            step "Do something"
                click "Start"
            on timeout
                screenshot
                abort "Task timed out"
            """;

        var task = ParseSource(source);

        Assert.NotNull(task.OnTimeout);
        Assert.Equal(2, task.OnTimeout.Actions.Count);
        Assert.IsType<ScreenshotAction>(task.OnTimeout.Actions[0]);
        Assert.IsType<AbortAction>(task.OnTimeout.Actions[1]);
        Assert.Equal("Task timed out", ((AbortAction)task.OnTimeout.Actions[1]).Message);
    }

    [Fact]
    public void Parse_OnErrorHandler_ParsesCorrectly()
    {
        var source = """
            task "With Error Handler"
            step "Do something"
                click "Start"
            on error
                screenshot
            """;

        var task = ParseSource(source);

        Assert.NotNull(task.OnError);
        Assert.Single(task.OnError.Actions);
        Assert.IsType<ScreenshotAction>(task.OnError.Actions[0]);
    }

    [Fact]
    public void Parse_BothHandlers_ParsesCorrectly()
    {
        var source = """
            task "With Both Handlers"
            step "Do something"
                click "Start"
            on timeout
                abort "Timed out"
            on error
                abort "Error occurred"
            """;

        var task = ParseSource(source);

        Assert.NotNull(task.OnTimeout);
        Assert.NotNull(task.OnError);
        Assert.IsType<AbortAction>(task.OnTimeout.Actions[0]);
        Assert.IsType<AbortAction>(task.OnError.Actions[0]);
    }

    [Fact]
    public void Parse_StepLevelTimeoutOverride_SetsTimeoutSeconds()
    {
        var source = """
            task "With Timeout"
            step "Slow step"
                timeout 60
                click "Process"
            """;

        var task = ParseSource(source);

        Assert.Equal(60, task.Steps[0].TimeoutSeconds);
    }

    [Fact]
    public void Parse_StepWithoutTimeout_HasNullTimeoutSeconds()
    {
        var source = """
            task "No Timeout"
            step "Normal step"
                click "Go"
            """;

        var task = ParseSource(source);

        Assert.Null(task.Steps[0].TimeoutSeconds);
    }

    [Fact]
    public void Parse_MissingTaskDeclaration_ThrowsMblParseException()
    {
        var source = """
            step "No task declaration"
                click "Button"
            """;

        Assert.Throws<MblParseException>(() => ParseSource(source));
    }

    [Fact]
    public void Parse_MissingStep_ThrowsOnUnexpectedToken()
    {
        var source = """
            task "No Steps"
            click "Button"
            """;

        Assert.Throws<MblParseException>(() => ParseSource(source));
    }

    [Fact]
    public void Parse_TypeAction_LiteralString_HasIsParamFalse()
    {
        var source = """
            task "Type Test"
            step "Type literal"
                type "hello world" into "Name Field"
            """;

        var task = ParseSource(source);
        var typeAction = Assert.IsType<TypeAction>(task.Steps[0].Actions[0]);

        Assert.Equal("hello world", typeAction.Value);
        Assert.False(typeAction.IsParam);
        Assert.Equal("Name Field", typeAction.Target);
    }

    [Fact]
    public void Parse_TypeAction_ParameterReference_HasIsParamTrue()
    {
        var source = """
            task "Type Test"
            input customer_name
            step "Type param"
                type customer_name into "Name Field"
            """;

        var task = ParseSource(source);
        var typeAction = Assert.IsType<TypeAction>(task.Steps[0].Actions[0]);

        Assert.Equal("customer_name", typeAction.Value);
        Assert.True(typeAction.IsParam);
        Assert.Equal("Name Field", typeAction.Target);
    }

    [Fact]
    public void Parse_TypeAction_WithAppend_HasAppendTrue()
    {
        var source = """
            task "Type Append"
            step "Append text"
                type "more text" append into "Notes"
            """;

        var task = ParseSource(source);
        var typeAction = Assert.IsType<TypeAction>(task.Steps[0].Actions[0]);

        Assert.True(typeAction.Append);
    }

    [Fact]
    public void Parse_SelectAction_LiteralString_HasIsParamFalse()
    {
        var source = """
            task "Select Test"
            step "Select literal"
                select "Option A" in "Dropdown"
            """;

        var task = ParseSource(source);
        var selectAction = Assert.IsType<SelectAction>(task.Steps[0].Actions[0]);

        Assert.Equal("Option A", selectAction.Value);
        Assert.False(selectAction.IsParam);
        Assert.Equal("Dropdown", selectAction.Target);
    }

    [Fact]
    public void Parse_SelectAction_ParameterReference_HasIsParamTrue()
    {
        var source = """
            task "Select Test"
            input payment_term
            step "Select param"
                select payment_term in "Payment Terms"
            """;

        var task = ParseSource(source);
        var selectAction = Assert.IsType<SelectAction>(task.Steps[0].Actions[0]);

        Assert.Equal("payment_term", selectAction.Value);
        Assert.True(selectAction.IsParam);
        Assert.Equal("Payment Terms", selectAction.Target);
    }

    [Fact]
    public void Parse_FullCreateInvoiceExample_ParsesCompletely()
    {
        var source = """
            task "create_invoice"
            input customer_name, invoice_number, amount

            step "Open invoice form"
                expect "ExportMaster main screen is visible"
                click "Invoices"
                expect "Invoice list is displayed"
                click "New Invoice"
                expect "New invoice form is open"

            step "Fill in invoice details"
                timeout 45
                type customer_name into "Customer Name"
                type invoice_number into "Invoice Number"
                type amount into "Amount"
                select "Net 30" in "Payment Terms"
                key Tab

            step "Save and verify"
                click "Save"
                expect "Invoice saved confirmation"
                extract confirmation_number from "Confirmation Number"
                output confirmation_number
                screenshot

            on timeout
                screenshot
                abort "Task timed out while creating invoice"

            on error
                screenshot
            """;

        var task = ParseSource(source, "create_invoice.mbl");

        Assert.Equal("create_invoice", task.Name);
        Assert.Equal("create_invoice.mbl", task.FileName);

        // Inputs
        Assert.Equal(3, task.Inputs.Count);
        Assert.Equal("customer_name", task.Inputs[0]);
        Assert.Equal("invoice_number", task.Inputs[1]);
        Assert.Equal("amount", task.Inputs[2]);

        // Steps
        Assert.Equal(3, task.Steps.Count);

        // Step 1: Open invoice form
        Assert.Equal("Open invoice form", task.Steps[0].Description);
        Assert.Null(task.Steps[0].TimeoutSeconds);
        Assert.Equal(5, task.Steps[0].Actions.Count);

        // Step 2: Fill in invoice details
        Assert.Equal("Fill in invoice details", task.Steps[1].Description);
        Assert.Equal(45, task.Steps[1].TimeoutSeconds);
        Assert.Equal(5, task.Steps[1].Actions.Count);

        // Verify parameter references in step 2
        var typeCustomer = Assert.IsType<TypeAction>(task.Steps[1].Actions[0]);
        Assert.Equal("customer_name", typeCustomer.Value);
        Assert.True(typeCustomer.IsParam);

        // Step 3: Save and verify
        Assert.Equal("Save and verify", task.Steps[2].Description);
        Assert.Equal(5, task.Steps[2].Actions.Count);

        // Verify extract and output
        var extract = Assert.IsType<ExtractAction>(task.Steps[2].Actions[3]);
        Assert.Equal("confirmation_number", extract.VariableName);
        Assert.Equal("Confirmation Number", extract.Source);

        var output = Assert.IsType<OutputAction>(task.Steps[2].Actions[4]);
        Assert.Equal("confirmation_number", output.VariableName);

        // Error handlers
        Assert.NotNull(task.OnTimeout);
        Assert.Equal(2, task.OnTimeout.Actions.Count);
        Assert.IsType<ScreenshotAction>(task.OnTimeout.Actions[0]);
        Assert.IsType<AbortAction>(task.OnTimeout.Actions[1]);

        Assert.NotNull(task.OnError);
        Assert.Single(task.OnError.Actions);
        Assert.IsType<ScreenshotAction>(task.OnError.Actions[0]);
    }

    [Fact]
    public void Parse_MultipleSteps_PreservesOrder()
    {
        var source = """
            task "Multi Step"
            step "First"
                click "A"
            step "Second"
                click "B"
            step "Third"
                click "C"
            """;

        var task = ParseSource(source);

        Assert.Equal(3, task.Steps.Count);
        Assert.Equal("First", task.Steps[0].Description);
        Assert.Equal("Second", task.Steps[1].Description);
        Assert.Equal("Third", task.Steps[2].Description);
    }
}
