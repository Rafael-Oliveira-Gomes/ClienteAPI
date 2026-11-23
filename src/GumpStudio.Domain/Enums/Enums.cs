namespace GumpStudio.Domain.Enums;

public enum UserRole { Admin = 1, Artist = 2 }
public enum AccessStatus { Allowed = 0, Inactive = 1, NotStarted = 2, Expired = 3 }
public enum ArtistType { Fixed = 1, Rotating = 2 }
public enum CompensationType { MonthlyFixed = 1, Daily = 2, Percentage = 3 }

public enum UnitOfMeasure
{
    Ml = 1, Pair = 2, Gram = 3, Box = 4,
    BatoqueP = 5, BatoqueM = 6, BatoqueG = 7, BatoqueGG = 8
}

public enum SessionStatus { Scheduled = 1, Confirmed = 2, InProgress = 3, Completed = 4, Cancelled = 5, NoShow = 6 }
public enum CalendarSyncStatus { NotSynced = 0, Synced = 1, Failed = 2 }

public enum StockMovementType
{
    Entry = 1, Usage = 2, Damaged = 3, InventoryAdjustment = 4,
    CorrectionEntry = 5, CorrectionExit = 6
}

public enum TransactionType { Income = 1, Expense = 2 }
public enum DocumentStatus { Pending = 1, Approved = 2, Rejected = 3 }
