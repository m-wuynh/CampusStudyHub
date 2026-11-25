using System.Reflection;
using Microsoft.Data.SqlClient;

namespace StudyHub.Flashcards.Tests;

internal static class SqlExceptionFactory
{
    private const BindingFlags InternalInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static SqlException Create(int number, string message)
    {
        // SqlClient only exposes these constructors internally, so build the error without opening a connection.
        var error = Construct<SqlError>(
            [typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(Exception)],
            [number, (byte)0, (byte)14, "test", message, "test", 1, null]);
        var errors = Construct<SqlErrorCollection>([], []);
        typeof(SqlErrorCollection).GetMethod("Add", InternalInstance)!.Invoke(errors, [error]);

        return Construct<SqlException>(
            [typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid)],
            [message, errors, null, Guid.Empty]);
    }

    private static T Construct<T>(Type[] parameterTypes, object?[] arguments) =>
        (T)typeof(T).GetConstructor(InternalInstance, binder: null, parameterTypes, modifiers: null)!.Invoke(arguments);
}
