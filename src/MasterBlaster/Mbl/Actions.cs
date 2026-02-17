namespace MasterBlaster.Mbl;

public interface IAction { }

public record ExpectAction(string Description) : IAction;
public record ClickAction(string Target) : IAction;
public record DoubleClickAction(string Target) : IAction;
public record RightClickAction(string Target) : IAction;
public record TypeAction(string Value, bool IsParam, string Target, bool Append) : IAction;
public record SelectAction(string Value, bool IsParam, string Target) : IAction;
public record KeyAction(string KeyCombo) : IAction;
public record ExtractAction(string VariableName, string Source) : IAction;
public record OutputAction(string VariableName) : IAction;
public record ScreenshotAction() : IAction;
public record AbortAction(string Message) : IAction;
public record IfScreenShowsAction(string Condition, List<IAction> Then, List<IAction>? Else) : IAction;
