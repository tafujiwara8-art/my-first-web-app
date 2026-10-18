using ClipFlow.Core.Editing;
using ClipFlow.Core.Imaging;
using ClipFlow.Core.Masking;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public class EditorDocumentTests
{
    [Fact]
    public void DragCreate_IsOneUndoStep()
    {
        var doc = new EditorDocument();
        var item = MaskItem.Begin(new PixelPoint(10, 10), MaskKind.SolidFill);

        doc.BeginChange();
        doc.Set(item);
        doc.Set(item.WithDragEnd(new PixelPoint(20, 20)));
        doc.Set(item.WithDragEnd(new PixelPoint(30, 25)));
        doc.EndChange();

        doc.Items.Should().ContainSingle().Which.Bounds.Should().Be(new PixelRect(10, 10, 20, 15));
        doc.Undo().Should().BeTrue();
        doc.Items.Should().BeEmpty();
        doc.Redo().Should().BeTrue();
        doc.Items.Should().ContainSingle();
    }

    [Fact]
    public void CancelChange_RestoresAndRecordsNothing()
    {
        var doc = new EditorDocument();
        doc.BeginChange();
        doc.Set(MaskItem.Begin(new PixelPoint(1, 1), MaskKind.SolidFill));
        doc.CancelChange();

        doc.Items.Should().BeEmpty();
        doc.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void MoveResizeDelete_AndKindChange()
    {
        var doc = new EditorDocument();
        var mask = new MaskItem(Guid.NewGuid(), new PixelRect(10, 10, 20, 20), MaskKind.SolidFill);
        doc.Apply(d => d.Set(mask));

        doc.Apply(d => d.Set(mask.Translate(5, -5)));
        doc.Find(mask.Id)!.Bounds.Should().Be(new PixelRect(15, 5, 20, 20));

        doc.Apply(d => d.Set(doc.Find(mask.Id)!.MoveHandle(3, new PixelPoint(50, 50))));
        doc.Find(mask.Id)!.Bounds.Should().Be(PixelRect.FromEdges(15, 5, 50, 50));

        doc.Apply(d => d.Set(((MaskItem)doc.Find(mask.Id)!) with { Kind = MaskKind.Blur }));
        doc.HasWeakMasks.Should().BeTrue();
        doc.BuildMasks().Single().Kind.Should().Be(MaskKind.Blur);

        doc.Apply(d => d.Remove(mask.Id));
        doc.Items.Should().BeEmpty();

        doc.Undo();
        doc.Undo();
        doc.Find(mask.Id)!.Should().BeOfType<MaskItem>().Which.Kind.Should().Be(MaskKind.SolidFill);
    }

    [Fact]
    public void ResizePastOppositeCorner_Normalizes()
    {
        var mask = new MaskItem(Guid.NewGuid(), new PixelRect(10, 10, 20, 20), MaskKind.SolidFill);

        var moved = mask.MoveHandle(0, new PixelPoint(40, 40)); // 左上を右下の外へ

        moved.Bounds.Should().Be(PixelRect.FromEdges(30, 30, 40, 40));
    }

    [Fact]
    public void HitTest_PrefersTopmost()
    {
        var doc = new EditorDocument();
        var bottom = new MaskItem(Guid.NewGuid(), new PixelRect(0, 0, 50, 50), MaskKind.SolidFill);
        var top = new MaskItem(Guid.NewGuid(), new PixelRect(20, 20, 50, 50), MaskKind.Mosaic);
        doc.Apply(d => { d.Set(bottom); d.Set(top); });

        doc.HitTest(new PixelPoint(30, 30), 0)!.Id.Should().Be(top.Id);
        doc.HitTest(new PixelPoint(5, 5), 0)!.Id.Should().Be(bottom.Id);
        doc.HitTest(new PixelPoint(200, 200), 0).Should().BeNull();
    }

    [Fact]
    public void Lock_PreventsFurtherEditingAndClearsHistory()
    {
        var doc = new EditorDocument();
        doc.Apply(d => d.Set(MaskItem.Begin(new PixelPoint(0, 0), MaskKind.SolidFill).WithDragEnd(new PixelPoint(5, 5))));

        doc.Lock();

        doc.CanUndo.Should().BeFalse();
        doc.Undo().Should().BeFalse();
        var act = () => doc.BeginChange();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Compose_AppliesMasksThenAnnotations()
    {
        using var src = TestImages.Pattern(40, 40);
        var doc = new EditorDocument();
        var red = Bgra.FromRgb(255, 0, 0);
        doc.Apply(d =>
        {
            d.Set(new MaskItem(Guid.NewGuid(), new PixelRect(0, 0, 40, 40), MaskKind.SolidFill));
            d.Set(new FrameItem(Guid.NewGuid(), new PixelRect(5, 5, 10, 10), red, 1));
        });

        using var result = doc.Compose(src);

        result.GetPixel(5, 5).Should().Be(red);
        result.GetPixel(30, 30).Should().Be(Bgra.Black);
        src.GetPixel(30, 30).Should().NotBe(Bgra.Black, "元画像は変更しない");
    }

    [Fact]
    public void ArrowAndFrame_HitTest()
    {
        var arrow = new ArrowItem(Guid.NewGuid(), new PixelPoint(0, 0), new PixelPoint(100, 0), Bgra.Black, 3);
        var frame = new FrameItem(Guid.NewGuid(), new PixelRect(0, 0, 100, 100), Bgra.Black, 3);

        arrow.HitTest(new PixelPoint(50, 2), 2).Should().BeTrue();
        arrow.HitTest(new PixelPoint(50, 30), 2).Should().BeFalse();
        frame.HitTest(new PixelPoint(1, 50), 2).Should().BeTrue();
        frame.HitTest(new PixelPoint(50, 50), 2).Should().BeFalse("枠の内側は当たらない");
    }

    [Fact]
    public void IsEntirelyBlack_DetectsBlackImages()
    {
        using var black = RawImage.Create(5, 5, Bgra.Black);
        using var pattern = TestImages.Pattern(5, 5);

        ImageAnalysis.IsEntirelyBlack(black).Should().BeTrue();
        ImageAnalysis.IsEntirelyBlack(pattern).Should().BeFalse();
    }
}
