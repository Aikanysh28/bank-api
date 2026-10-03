namespace BankApi.Domain;

public enum PhoneType
{
    Mobile,
    Home,
    Work
}

public enum Currency
{
    KGS,
    USD,
    EUR,
    RUB,
    KZT
}

public enum AccountStatus
{
    Open,
    Closed
}

public enum CardType
{
    Debit,
    Credit
}

public enum CardStatus
{
    Active,
    Blocked
}

public enum UserRole
{
    Operator,
    Admin
}
