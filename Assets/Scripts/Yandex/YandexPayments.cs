using System;
using System.Collections.Generic;
using System;
using UnityEngine;

public static class YandexPayments
{
    public const string SeasonPassProduct = YandexConfig.ProductSeasonPass;
    public static event Action PurchasesChanged;

    private static readonly HashSet<string> _owned = new HashSet<string>();
    private static bool _loading;
    private static Action<bool> _buyCallback;

    public static bool HasSeasonPass => _owned.Contains(SeasonPassProduct) || SeasonPass.IsPremium;

    public static void BeginLoad()
    {
        if (!YandexGames.Ready) return;
        if (_loading) return;
        _loading = true;
        YandexSdk.LoadPayments();
    }

    public static void BuySeasonPass(Action<bool> done)
    {
        _buyCallback = done;
        if (!YandexGames.Ready)
        {
            _buyCallback?.Invoke(false);
            return;
        }
        YandexSdk.Purchase(SeasonPassProduct);
    }

    public static void Restore()
    {
        BeginLoad();
    }

    public static void HandlePurchasesLoaded(string json)
    {
        _loading = false;
        _owned.Clear();
        try
        {
            PurchaseList list = JsonUtility.FromJson<PurchaseList>("{\"items\":" + json + "}");
            if (list.items != null)
            {
                for (int i = 0; i < list.items.Length; i++)
                {
                    string id = list.items[i].productID;
                    if (!string.IsNullOrEmpty(id))
                    {
                        _owned.Add(id);
                        if (id == SeasonPassProduct)
                        {
                            SeasonPass.SetPremium(true);
                        }
                    }
                }
            }
        }
        catch
        {
        }
        PurchasesChanged?.Invoke();
    }

    public static void HandlePurchaseResult(string json)
    {
        bool ok = false;
        try
        {
            PurchaseResult pr = JsonUtility.FromJson<PurchaseResult>(json);
            if (pr != null && !string.IsNullOrEmpty(pr.productID))
            {
                _owned.Add(pr.productID);
                if (pr.productID == SeasonPassProduct)
                {
                    SeasonPass.SetPremium(true);
                }
                ok = true;
            }
        }
        catch
        {
        }
        _buyCallback?.Invoke(ok);
        _buyCallback = null;
        PurchasesChanged?.Invoke();
    }

    public static void HandlePurchaseError(string msg)
    {
        _buyCallback?.Invoke(false);
        _buyCallback = null;
        PurchasesChanged?.Invoke();
    }

    /// <summary>
    /// Не удалось загрузить список покупок. Отдельно от HandlePurchaseError:
    /// тот вызывается при отказе самой покупки. Здесь важно снять _loading,
    /// иначе BeginLoad навсегда решит, что загрузка уже идёт, и после первой
    /// же ошибки (а у нового игрока покупок нет — это штатный случай)
    /// восстановление покупок перестанет работать вовсе.
    /// </summary>
    public static void HandlePurchasesError(string msg)
    {
        _loading = false;
        _buyCallback?.Invoke(false);
        _buyCallback = null;
        PurchasesChanged?.Invoke();
    }

    [Serializable]
    private class PurchaseItem
    {
        public string productID;
        public string purchaseToken;
    }

    [Serializable]
    private class PurchaseList
    {
        public PurchaseItem[] items;
    }

    [Serializable]
    private class PurchaseResult
    {
        public string productID;
        public string purchaseToken;
    }
}
