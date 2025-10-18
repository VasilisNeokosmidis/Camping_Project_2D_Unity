using UnityEngine;
using System;

public class EconomyWallet : MonoBehaviour
{
    public static EconomyWallet Instance { get; private set; }

    [Header("Starting Values")]
    [SerializeField] private float startingBalance = 500f;

    public float AccountBalance { get; private set; }
    public float AccountsPayable { get; private set; }

    public event Action OnMoneyChanged;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        AccountBalance = startingBalance;
        AccountsPayable = 0f;
        OnMoneyChanged?.Invoke();
    }

    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (AccountBalance + 0.0005f >= amount)
        {
            AccountBalance -= amount;
            OnMoneyChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void AddToAccountsPayable(float amount)
    {
        if (amount <= 0f) return;
        AccountsPayable += amount;
        OnMoneyChanged?.Invoke();
    }

    public void SetBalances(float accountBalance, float accountsPayable)
    {
        AccountBalance = Mathf.Max(0f, accountBalance);
        AccountsPayable = Mathf.Max(0f, accountsPayable);
        OnMoneyChanged?.Invoke();
    }
}
