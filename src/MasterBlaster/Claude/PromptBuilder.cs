namespace MasterBlaster.Claude;

public static class PromptBuilder
{
    public static string BuildSystemPrompt(int width, int height) =>
        $"""
        You are an automation assistant controlling a legacy Windows application called ExportMaster through an RDP session. You are looking at a screenshot of the application and will be asked to perform specific actions.

        The screenshot resolution is {width}x{height} pixels. When asked to identify UI elements, respond with precise pixel coordinates. When asked to read text, respond with the exact text visible on screen.

        Be precise. Legacy Windows applications have small click targets — buttons, menu items, and fields may be close together. Identify the correct element carefully.
        """;

    public static string BuildExpectPrompt(string description) =>
        $"""
        Look at this screenshot. Does the following description match what you see?

        Description: "{description}"

        Respond with exactly one of:
        MATCH - if the description accurately reflects the current screen state
        NO_MATCH - if the screen clearly shows something different
        UNCERTAIN - if you cannot confidently determine either way

        If NO_MATCH, on the next line briefly describe what you actually see.
        """;

    public static string BuildClickPrompt(string target) =>
        $"""
        Look at this screenshot. I need to click on the following element:

        Element: "{target}"

        Respond with the pixel coordinates of the centre of this element in the format:
        x,y

        If you cannot find this element, respond with:
        NOT_FOUND: brief description of what you see instead
        """;

    public static string BuildTypeFieldPrompt(string target) =>
        $"""
        Look at this screenshot. I need to type text into the following field:

        Field: "{target}"

        Respond with the pixel coordinates of the centre of this field in the format:
        x,y

        If you cannot find this field, respond with:
        NOT_FOUND: brief description of what you see instead
        """;

    public static string BuildSelectDropdownPrompt(string target) =>
        $"""
        Look at this screenshot. I need to select a value from the following dropdown:

        Dropdown: "{target}"

        Respond with the pixel coordinates of the centre of this dropdown in the format:
        x,y

        If you cannot find this dropdown, respond with:
        NOT_FOUND: brief description of what you see instead
        """;

    public static string BuildSelectOptionPrompt(string value) =>
        $"""
        Look at this screenshot. The dropdown is now open. I need to select:

        Option: "{value}"

        Respond with the pixel coordinates of this option in the format:
        x,y

        If you cannot find this option in the dropdown, respond with:
        NOT_FOUND: list the options you can see
        """;

    public static string BuildExtractPrompt(string source) =>
        $"""
        Look at this screenshot. I need to read the value from:

        Field: "{source}"

        Respond with just the text value you can see in this field.
        If the field is empty, respond with: EMPTY
        If you cannot find the field, respond with: NOT_FOUND
        """;

    public static string BuildIfScreenShowsPrompt(string condition) =>
        $"""
        Look at this screenshot. Is the following visible?

        Condition: "{condition}"

        Respond with exactly: YES or NO
        """;
}
