using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Infrastructure
{
    public sealed class AddressablesCatalogService : IAddressablesCatalogService
    {
        public bool IsInitialized { get; private set; }

        public async UniTask InitializeAndUpdateAsync(CancellationToken ct)
        {
            if (IsInitialized)
                return;

            ct.ThrowIfCancellationRequested();

            var initHandle = Addressables.InitializeAsync(autoReleaseHandle: false);
            try
            {
                await initHandle.ToUniTask(cancellationToken: ct);
                if (initHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Addressables.InitializeAsync failed. Status: {initHandle.Status}");
                }
            }
            finally
            {
                if (initHandle.IsValid())
                    Addressables.Release(initHandle);
            }

            await TryUpdateCatalogsAsync(ct);

            IsInitialized = true;
        }

        private static async UniTask TryUpdateCatalogsAsync(CancellationToken ct)
        {
            var checkHandle = Addressables.CheckForCatalogUpdates(autoReleaseHandle: false);
            try
            {
                await checkHandle.ToUniTask(cancellationToken: ct);

                if (checkHandle.Status != AsyncOperationStatus.Succeeded || checkHandle.Result == null || checkHandle.Result.Count == 0)
                    return;

                var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, autoReleaseHandle: false);
                try
                {
                    await updateHandle.ToUniTask(cancellationToken: ct);
                    if (updateHandle.Status != AsyncOperationStatus.Succeeded)
                    {
                        Debug.LogWarning($"[AddressablesCatalogService] UpdateCatalogs failed. Status: {updateHandle.Status}. Continuing with current catalog.");
                    }
                    else
                    {
                        Debug.Log($"[AddressablesCatalogService] Catalogs updated ({checkHandle.Result.Count}).");
                    }
                }
                finally
                {
                    if (updateHandle.IsValid())
                        Addressables.Release(updateHandle);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesCatalogService] Catalog update skipped: {ex.Message}");
            }
            finally
            {
                if (checkHandle.IsValid())
                    Addressables.Release(checkHandle);
            }
        }
    }
}
