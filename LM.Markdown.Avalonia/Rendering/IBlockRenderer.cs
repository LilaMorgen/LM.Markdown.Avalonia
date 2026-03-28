using Avalonia.Controls;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering;

public interface IBlockRenderer
{
    bool CanRender(Block block);
    Control Render(Block block, RenderContext context);
}

public abstract class BlockRenderer<TBlock> : IBlockRenderer where TBlock : Block
{
    public virtual bool CanRender(Block block) => block is TBlock;

    public Control Render(Block block, RenderContext context) => RenderBlock((TBlock)block, context);

    protected abstract Control RenderBlock(TBlock block, RenderContext context);
}
