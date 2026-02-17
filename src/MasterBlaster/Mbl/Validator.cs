namespace MasterBlaster.Mbl;

public class Validator
{
    public List<string> Validate(TaskDefinition task)
    {
        var errors = new List<string>();

        ValidateAtLeastOneStep(task, errors);
        ValidateOutputVariables(task, errors);
        ValidateParameterReferences(task, errors);
        ValidateNoNestedIfs(task, errors);

        return errors;
    }

    private static void ValidateAtLeastOneStep(TaskDefinition task, List<string> errors)
    {
        if (task.Steps.Count == 0)
        {
            errors.Add("Task must contain at least one step.");
        }
    }

    /// <summary>
    /// Validates that all 'output' variable names have a corresponding 'extract' that appears
    /// earlier in document order (same step before the output, or in any earlier step).
    /// </summary>
    private static void ValidateOutputVariables(TaskDefinition task, List<string> errors)
    {
        var extractedVariables = new HashSet<string>();

        for (int stepIndex = 0; stepIndex < task.Steps.Count; stepIndex++)
        {
            var step = task.Steps[stepIndex];
            ValidateOutputVariablesInActionList(step.Actions, extractedVariables, step.Description, errors);
        }

        // Also check error handler actions
        if (task.OnTimeout != null)
        {
            ValidateOutputVariablesInActionList(task.OnTimeout.Actions, extractedVariables, "on timeout handler", errors);
        }

        if (task.OnError != null)
        {
            ValidateOutputVariablesInActionList(task.OnError.Actions, extractedVariables, "on error handler", errors);
        }
    }

    private static void ValidateOutputVariablesInActionList(
        List<IAction> actions,
        HashSet<string> extractedVariables,
        string context,
        List<string> errors)
    {
        foreach (var action in actions)
        {
            if (action is ExtractAction extract)
            {
                extractedVariables.Add(extract.VariableName);
            }
            else if (action is OutputAction output)
            {
                if (!extractedVariables.Contains(output.VariableName))
                {
                    errors.Add(
                        $"Variable '{output.VariableName}' is used in 'output' in {context} " +
                        $"but has not been extracted by a preceding 'extract' action.");
                }
            }
            else if (action is IfScreenShowsAction ifAction)
            {
                // Process then/else branches - variables extracted inside branches
                // are still visible after the block in document order
                ValidateOutputVariablesInActionList(ifAction.Then, extractedVariables, context, errors);
                if (ifAction.Else != null)
                {
                    ValidateOutputVariablesInActionList(ifAction.Else, extractedVariables, context, errors);
                }
            }
        }
    }

    /// <summary>
    /// Validates that all parameter references (IsParam == true) in type and select actions
    /// are declared in the task's input declaration.
    /// </summary>
    private static void ValidateParameterReferences(TaskDefinition task, List<string> errors)
    {
        var declaredInputs = new HashSet<string>(task.Inputs);

        foreach (var step in task.Steps)
        {
            ValidateParameterReferencesInActionList(step.Actions, declaredInputs, step.Description, errors);
        }

        if (task.OnTimeout != null)
        {
            ValidateParameterReferencesInActionList(task.OnTimeout.Actions, declaredInputs, "on timeout handler", errors);
        }

        if (task.OnError != null)
        {
            ValidateParameterReferencesInActionList(task.OnError.Actions, declaredInputs, "on error handler", errors);
        }
    }

    private static void ValidateParameterReferencesInActionList(
        List<IAction> actions,
        HashSet<string> declaredInputs,
        string context,
        List<string> errors)
    {
        foreach (var action in actions)
        {
            switch (action)
            {
                case TypeAction typeAction when typeAction.IsParam:
                    if (!declaredInputs.Contains(typeAction.Value))
                    {
                        errors.Add(
                            $"Parameter '{typeAction.Value}' in 'type' action in {context} " +
                            $"is not declared in 'input'.");
                    }
                    break;

                case SelectAction selectAction when selectAction.IsParam:
                    if (!declaredInputs.Contains(selectAction.Value))
                    {
                        errors.Add(
                            $"Parameter '{selectAction.Value}' in 'select' action in {context} " +
                            $"is not declared in 'input'.");
                    }
                    break;

                case IfScreenShowsAction ifAction:
                    ValidateParameterReferencesInActionList(ifAction.Then, declaredInputs, context, errors);
                    if (ifAction.Else != null)
                    {
                        ValidateParameterReferencesInActionList(ifAction.Else, declaredInputs, context, errors);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Validates that 'if' blocks are not nested (no 'if' inside another 'if').
    /// </summary>
    private static void ValidateNoNestedIfs(TaskDefinition task, List<string> errors)
    {
        foreach (var step in task.Steps)
        {
            CheckForNestedIfs(step.Actions, false, step.Description, errors);
        }

        if (task.OnTimeout != null)
        {
            CheckForNestedIfs(task.OnTimeout.Actions, false, "on timeout handler", errors);
        }

        if (task.OnError != null)
        {
            CheckForNestedIfs(task.OnError.Actions, false, "on error handler", errors);
        }
    }

    private static void CheckForNestedIfs(
        List<IAction> actions,
        bool insideIf,
        string context,
        List<string> errors)
    {
        foreach (var action in actions)
        {
            if (action is IfScreenShowsAction ifAction)
            {
                if (insideIf)
                {
                    errors.Add(
                        $"Nested 'if' blocks are not allowed. " +
                        $"Found nested 'if screen shows \"{ifAction.Condition}\"' in {context}.");
                }
                else
                {
                    // Check children with insideIf = true
                    CheckForNestedIfs(ifAction.Then, true, context, errors);
                    if (ifAction.Else != null)
                    {
                        CheckForNestedIfs(ifAction.Else, true, context, errors);
                    }
                }
            }
        }
    }
}
