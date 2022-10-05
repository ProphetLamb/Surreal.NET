using System.Text.Json.Serialization;

namespace SurrealDB.Shared.Tests;

public class TestObject<TKey, TValue> {
    [JsonConstructor]
    public TestObject(TKey key, TValue value) {
        Key = key;
        Value = value;
    }

    public TKey Key { get; set; }
    public TValue Value { get; set; }
}

public class ExtendedTestObject<TKey, TValue> : TestObject<TKey, TValue> {
    [JsonConstructor]
    public ExtendedTestObject(TKey key, TValue value, TValue mergeValue) : base (key, value) {
        MergeValue = mergeValue;
    }
    
    public TValue MergeValue { get; set; }
}

public readonly record struct IdScopeAuth(
    string id,
    string user,
    string pass,
    string NS,
    string DB,
    string SC) : IAuth;

public readonly record struct User(
    string id,
    string email);

public class Temperature {
    public string location;
    public DateTime date;
    public float temperature;
}

public class Field<T> {
    public List<T> field { get; set; }
}
