using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Inventory.UseHandlers
{
    /// <summary>
    /// Stub: real puzzle assembly (with per-puzzle piece counts from config and a reward) lands
    /// with the puzzle feature. For now we just count how many pieces the player owns under the
    /// puzzle_piece category and log a progress message.
    /// </summary>
    public sealed class PuzzleAssembleUseHandler : IInventoryItemUseHandler
    {
        // TODO: replace with per-puzzle config when the puzzle feature ships.
        private const int FullPiecesStub = 8;

        private readonly IInventoryService _inventory;

        public PuzzleAssembleUseHandler(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        public string SupportedCategoryId => InventoryCategories.PuzzlePiece;

        public UniTask<InventoryUseResult> UseAsync(InventoryItem item, CancellationToken ct)
        {
            var collected = 0;
            var pieces = _inventory.GetByCategory(InventoryCategories.PuzzlePiece);
            for (var i = 0; i < pieces.Count; i++) collected += pieces[i].Count;

            if (collected >= FullPiecesStub)
            {
                Debug.Log($"[Puzzle] assembled stub! collected={collected}, required={FullPiecesStub}");
                return UniTask.FromResult(InventoryUseResult.Ok(consume: false, message: "assembled (stub)"));
            }

            Debug.Log($"[Puzzle] progress {collected}/{FullPiecesStub} for piece {item.ItemId}");
            return UniTask.FromResult(InventoryUseResult.Ok(consume: false, message: $"{collected}/{FullPiecesStub}"));
        }
    }
}
