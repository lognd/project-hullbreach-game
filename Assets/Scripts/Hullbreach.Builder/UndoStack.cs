using System;

namespace Hullbreach.Builder
{
    /// <summary>Undo for the builder. S31 requires at least ten deep.</summary>
    public sealed class UndoStack
    {
        public const int MinimumDepth = 10;

        // TODO [B3]: Record a placement or removal so it can be reversed.
        //            Store the block that was there, not just the key -- undoing
        //            a removal has to restore type, modifiers AND damage.
        public void RecordPlace(int key) => throw new NotImplementedException();

        public void RecordRemove(int key, Hullbreach.Core.Block removed)
            => throw new NotImplementedException();

        // TODO [B3]
        public bool TryUndo(Hullbreach.Core.BlockGrid grid)
            => throw new NotImplementedException();

        public int Depth => throw new NotImplementedException();
    }
}
