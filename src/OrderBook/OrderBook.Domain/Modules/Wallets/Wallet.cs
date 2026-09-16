using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Wallets;

public sealed class Wallet
{
    public Wallet(UserId userId, long brlAvailable, long vibraniumAvailable) { if (brlAvailable < 0 || vibraniumAvailable < 0) throw new DomainException("Wallet balances cannot be negative."); UserId = userId; BrlAvailable = brlAvailable; VibraniumAvailable = vibraniumAvailable; }
    public static Wallet Rehydrate(UserId userId, long brlAvailable, long brlLocked, long vibraniumAvailable, long vibraniumLocked)
    {
        if (brlAvailable < 0 || brlLocked < 0 || vibraniumAvailable < 0 || vibraniumLocked < 0) throw new DomainException("Wallet balances cannot be negative.");
        return new Wallet(userId, brlAvailable, vibraniumAvailable) { BrlLocked = brlLocked, VibraniumLocked = vibraniumLocked };
    }
    public UserId UserId { get; }
    public long BrlAvailable { get; private set; }
    public long BrlLocked { get; private set; }
    public long VibraniumAvailable { get; private set; }
    public long VibraniumLocked { get; private set; }
    public void ReserveBuy(long amount) { (BrlAvailable, BrlLocked) = Move(BrlAvailable, BrlLocked, amount); }
    public void ReserveSell(long amount) { (VibraniumAvailable, VibraniumLocked) = Move(VibraniumAvailable, VibraniumLocked, amount); }
    public void CaptureBuy(long amount) { (BrlLocked, BrlAvailable) = Move(BrlLocked, BrlAvailable, amount); }
    public void CaptureSell(long amount) { (VibraniumLocked, VibraniumAvailable) = Move(VibraniumLocked, VibraniumAvailable, amount); }
    public void CreditBrl(long amount) { if (amount < 0) throw new DomainException("Amount cannot be negative."); BrlAvailable = checked(BrlAvailable + amount); }
    public void CreditVibranium(long amount) { if (amount < 0) throw new DomainException("Amount cannot be negative."); VibraniumAvailable = checked(VibraniumAvailable + amount); }
    public void ReleaseBrl(long amount) { (BrlLocked, BrlAvailable) = Move(BrlLocked, BrlAvailable, amount); }
    public void ReleaseVibranium(long amount) { (VibraniumLocked, VibraniumAvailable) = Move(VibraniumLocked, VibraniumAvailable, amount); }
    private static (long From, long To) Move(long from, long to, long amount) { if (amount < 0 || from < amount) throw new DomainException("Insufficient wallet balance."); try { return (checked(from - amount), checked(to + amount)); } catch (OverflowException) { throw new DomainException("Wallet arithmetic overflow."); } }
}
