using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class PreWorkflowTicketReferenceDisplayTest
{
    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("ChangeId", true)]
    public void IsActive_DependsOnNonEmptyLabel(string? label, bool expected)
    {
        Assert.That(PreWorkflowTicketReferenceDisplay.IsActive(label), Is.EqualTo(expected));
    }

    [TestCase(null, "-")]
    [TestCase("", "-")]
    [TestCase("   ", "-")]
    [TestCase(" CHG-42 ", "CHG-42")]
    public void DisplayValue_ShowsDashForEmptyReference(string? reference, string expected)
    {
        Assert.That(PreWorkflowTicketReferenceDisplay.DisplayValue(reference), Is.EqualTo(expected));
    }

    [Test]
    public void TicketHeadline_WithLabelAndReference_ShowsReference()
    {
        WfTicket ticket = new() { Title = "Open port", PreWorkflowTicketReference = " CHG-42 " };

        string headline = PreWorkflowTicketReferenceDisplay.TicketHeadline("Ticket", "ChangeId", ticket);

        Assert.That(headline, Is.EqualTo("Ticket CHG-42: Open port"));
    }

    [Test]
    public void TicketHeadline_WithoutLabel_HidesReference()
    {
        WfTicket ticket = new() { Title = "Open port", PreWorkflowTicketReference = "CHG-42" };

        string headline = PreWorkflowTicketReferenceDisplay.TicketHeadline("Ticket", "", ticket);

        Assert.That(headline, Is.EqualTo("Ticket: Open port"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void TicketHeadline_WithoutReference_ShowsPlainHeadline(string? reference)
    {
        WfTicket ticket = new() { Title = "Open port", PreWorkflowTicketReference = reference };

        string headline = PreWorkflowTicketReferenceDisplay.TicketHeadline("Ticket", "ChangeId", ticket);

        Assert.That(headline, Is.EqualTo("Ticket: Open port"));
    }
}
