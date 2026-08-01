using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Slip39Demo.UI.Shared;
using Xunit;

namespace Slip39Demo.Tests.Web;

// Regression tests for the Recoverer's input components. The original bug: both
// used a plain @bind on a [Parameter], which mutates only the child's local copy
// and never fires the ...Changed callback — so typed/pasted text never reached the
// parent, and Recover reported "Paste at least one SLIP-39 mnemonic" against a full
// textarea. These pin the two-way binding: an oninput on the textarea MUST push the
// new value up through the change callback.
public class RecoveryInputBindingTests : TestContext
{
    [Fact]
    public void MnemonicInput_Input_PropagatesToParentViaCallback()
    {
        string? captured = null;
        var cut = RenderComponent<MnemonicInput>(p => p
            .Add(x => x.MnemonicText, "")
            .Add(x => x.MnemonicTextChanged, EventCallback.Factory.Create<string>(this, v => captured = v)));

        cut.Find("textarea").Input("share one words\nshare two words");

        captured.Should().Be("share one words\nshare two words");
    }

    [Fact]
    public void CiphertextInput_Input_PropagatesToParentViaCallback()
    {
        string? captured = null;
        var cut = RenderComponent<CiphertextInput>(p => p
            .Add(x => x.ArmorText, "")
            .Add(x => x.ArmorTextChanged, EventCallback.Factory.Create<string>(this, v => captured = v)));

        cut.Find("textarea").Input("-----BEGIN AGE ENCRYPTED FILE-----");

        captured.Should().Be("-----BEGIN AGE ENCRYPTED FILE-----");
    }
}
