using System.Collections.Generic;

namespace VoidCraftLauncher.Models;

public sealed class InstanceOverviewDescriptionDocument
{
    public string Intro { get; set; } = "";

    public List<InstanceOverviewDescriptionSection> Sections { get; set; } = new();

    public string PlainText { get; set; } = "";
}