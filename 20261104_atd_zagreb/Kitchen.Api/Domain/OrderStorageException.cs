namespace Kitchen.Api.Domain;

public sealed class OrderStorageException(Exception innerException)
    : Exception("Could not save the order.", innerException);
