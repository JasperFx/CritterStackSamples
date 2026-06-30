namespace TripMessages;

// Domain exception types used to demonstrate Wolverine's error-handling policies in TripMessageHandler
// (retry-with-cooldown vs. requeue vs. move-to-error-queue). CritterWatch surfaces the resulting
// dead-letter + retry activity, so keeping a couple of distinct exception types makes the
// error-handling story visible in the console.

public class TransientException(string? message) : Exception(message);

public class OtherTransientException(string? message) : Exception(message);

public class RepairShopTooBusyException(string? message) : Exception(message);
