using FWO.Api.Client;
using System.Reflection;

namespace FWO.Test
{
    internal static class WorkflowConfigurationComponentTestSupport
    {
        private const BindingFlags kInstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Sets a component field without assigning Blazor parameters directly.
        /// </summary>
        public static void SetField(object instance, string fieldName, object? value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, kInstanceFlags)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            field.SetValue(instance, value);
        }

        /// <summary>
        /// Gets a component field for state assertions.
        /// </summary>
        public static T GetField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, kInstanceFlags)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            object? value = field.GetValue(instance);
            if (value == null)
            {
                if (default(T) is null)
                {
                    return default!;
                }
                throw new InvalidOperationException($"Field {fieldName} is null.");
            }
            return (T)value;
        }

        /// <summary>
        /// Gets a component field that may intentionally be null.
        /// </summary>
        public static object? GetFieldValue(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, kInstanceFlags)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            return field.GetValue(instance);
        }

        /// <summary>
        /// Sets an injected or parameter property through reflection.
        /// </summary>
        public static void SetProperty(object instance, string propertyName, object? value)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, kInstanceFlags)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            property.SetValue(instance, value);
        }

        /// <summary>
        /// Gets a component property for state assertions.
        /// </summary>
        public static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, kInstanceFlags)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            return (T)(property.GetValue(instance) ?? throw new InvalidOperationException($"Property {propertyName} is null."));
        }

        /// <summary>
        /// Invokes a private component method.
        /// </summary>
        public static object? Invoke(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, kInstanceFlags)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            return method.Invoke(instance, parameters);
        }

        /// <summary>
        /// Invokes a private asynchronous component method.
        /// </summary>
        public static async Task InvokeAsync(object instance, string methodName, params object?[] parameters)
        {
            Task task = (Task)(Invoke(instance, methodName, parameters)
                ?? throw new InvalidOperationException($"Method {methodName} returned null."));
            await task;
        }
    }

    internal sealed class RecordingWorkflowApiConnection : SimulatedApiConnection
    {
        private readonly Dictionary<string, object> responses = [];

        public List<(string Query, object? Variables, Type ResponseType)> Calls { get; } = [];

        /// <summary>
        /// Registers the result returned for an exact GraphQL query string.
        /// </summary>
        public void Respond<T>(string query, T response) where T : notnull => responses[query] = response;

        public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            Calls.Add((query, variables, typeof(T)));
            if (responses.TryGetValue(query, out object? response))
            {
                return Task.FromResult((T)response);
            }
            if (typeof(T) == typeof(object))
            {
                return Task.FromResult((T)new object());
            }
            throw new InvalidOperationException($"No test response registered for query returning {typeof(T).Name}.");
        }
    }
}
