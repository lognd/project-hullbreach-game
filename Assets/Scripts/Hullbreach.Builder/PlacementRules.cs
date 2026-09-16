using System;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    /// <summary>Placement validity for the two-click builder (S30).</summary>
    public static class PlacementRules
    {
        // TODO [B1]: A cell is valid when it is empty, in range, and shares at
        //            least one edge with an existing block. S30's open question
        //            -- whether "valid" needs anything special near the core --
        //            gets answered here.
        public static bool CanPlace(BlockGrid grid, int key)
            => throw new NotImplementedException();

        // TODO [B3]: A block can be removed when it is not the core. Whether a
        //            removal that would strand other blocks is refused outright
        //            or allowed (and the stranded blocks detach) is S31's open
        //            question. Use Articulation to answer it cheaply.
        public static bool CanRemove(BlockGrid grid, int key)
            => throw new NotImplementedException();
    }
}
