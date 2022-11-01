
// ReSharper disable All

using SurrealDB.Models.Result;

#pragma warning disable CS0169

namespace SurrealDB.Driver.Tests.Queries;
public class RestAuthQueryTests : AuthQueryTests<DatabaseRest> {
    public RestAuthQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RpcAuthQueryTests : AuthQueryTests<DatabaseRpc> {
    public RpcAuthQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

[Collection("SurrealDBRequired")]
public abstract class AuthQueryTests<T>
    where T : IDatabase, IDisposable, new() {

    protected readonly ITestOutputHelper Logger;

    public AuthQueryTests(ITestOutputHelper logger) {
        Logger = logger;
    }

#region Root Auth

    [Fact]
    public async Task SignInRootAuthTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var signinObject = new RootAuth(TestHelper.User, TestHelper.Pass);
            var response = await db.Signin(signinObject);
            TestHelper.AssertOk(response);
        }
    );

    [Fact]
    public async Task SignInRootAuthErrorTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var signinObject = new RootAuth(TestHelper.User, "WrongPassword");
            var response = await db.Signin(signinObject);
            TestHelper.AssertError(response);
        }
    );

#endregion Root Auth

#region Namespace User Auth

    [Fact]
    public async Task SignInNamespaceUserTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var user = "DatabaseUser";
            var password = "TestPassword";

            string sql = $"DEFINE LOGIN {user} ON NAMESPACE PASSWORD '{password}';";
            var queryResponse = await db.Query(sql, null);
            TestHelper.AssertOk(queryResponse);

            var signinObject = new NamespaceAuth(user, password, TestHelper.Namespace);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertOk(signinResponse);
            ResultValue result = signinResponse.FirstValue();
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();

            var authenticateResponse = await db.Authenticate(signinJwt!);
            TestHelper.AssertOk(authenticateResponse);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            TestHelper.AssertOk(tokenQueryResponse);
            ResultValue tokenQueryResult = tokenQueryResponse.FirstValue();
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.ID.Should().Be(user);
            token.Value.NS.Should().Be(TestHelper.Namespace);

            var invalidateResponse = await db.Invalidate();
            TestHelper.AssertOk(invalidateResponse);

            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Query(tokenSql, null));
        }
    );

    [Fact]
    public async Task SignInNamespaceUserThenOpenConnectionWithJwtTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var user = "DatabaseUser";
            var password = "TestPassword";

            string sql = $"DEFINE LOGIN {user} ON NAMESPACE PASSWORD '{password}';";
            var queryResponse = await db.Query(sql, null);
            TestHelper.AssertOk(queryResponse);

            var signinObject = new NamespaceAuth(user, password, TestHelper.Namespace);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertOk(signinResponse);
            ResultValue result = signinResponse.FirstValue();
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();

            var config = TestHelper.Default;
            config.Username = null;
            config.Password = null;
            config.JsonWebToken = signinJwt;
            await db.Close();
            db.Dispose();
            db = new();
            await db.Open(config);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            TestHelper.AssertOk(tokenQueryResponse);
            ResultValue tokenQueryResult = tokenQueryResponse.FirstValue();
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.ID.Should().Be(user);
            token.Value.NS.Should().Be(TestHelper.Namespace);

            var invalidateResponse = await db.Invalidate();
            TestHelper.AssertOk(invalidateResponse);

            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Query(tokenSql, null));
        }
    );

    [Fact]
    public async Task SignInNamespaceUserErrorTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var user = "DatabaseUser";
            var password = "TestPassword";

            var signinObject = new NamespaceAuth(user, password, TestHelper.Namespace);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertError(signinResponse);
        }
    );
    
#endregion Namespace User Auth

#region Database User Auth

    [Fact]
    public async Task SignInDatabaseUserTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var user = "DatabaseUser";
            var password = "TestPassword";

            string sql = $"DEFINE LOGIN {user} ON DATABASE PASSWORD '{password}';";
            var queryResponse = await db.Query(sql, null);
            TestHelper.AssertOk(queryResponse);

            var signinObject = new DatabaseAuth(user, password, TestHelper.Namespace, TestHelper.Database);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertOk(signinResponse);
            ResultValue result = signinResponse.FirstValue();
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();

            var authenticateResponse = await db.Authenticate(signinJwt!);
            TestHelper.AssertOk(authenticateResponse);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            TestHelper.AssertOk(tokenQueryResponse);
            ResultValue tokenQueryResult = tokenQueryResponse.FirstValue();
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.ID.Should().Be(user);
            token.Value.NS.Should().Be(TestHelper.Namespace);
            token.Value.DB.Should().Be(TestHelper.Database);

            var invalidateResponse = await db.Invalidate();
            TestHelper.AssertOk(invalidateResponse);

            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Query(tokenSql, null));
        }
    );

    [Fact]
    public async Task SignInDatabaseUserThenOpenConnectionWithJwtTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var user = "DatabaseUser";
            var password = "TestPassword";

            string sql = $"DEFINE LOGIN {user} ON DATABASE PASSWORD '{password}';";
            var queryResponse = await db.Query(sql, null);
            TestHelper.AssertOk(queryResponse);

            var signinObject = new DatabaseAuth(user, password, TestHelper.Namespace, TestHelper.Database);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertOk(signinResponse);
            ResultValue result = signinResponse.FirstValue();
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();

            var config = TestHelper.Default;
            config.Username = null;
            config.Password = null;
            config.JsonWebToken = signinJwt;
            await db.Close();
            db.Dispose();
            db = new();
            await db.Open(config);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            TestHelper.AssertOk(tokenQueryResponse);
            ResultValue tokenQueryResult = tokenQueryResponse.FirstValue();
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.ID.Should().Be(user);
            token.Value.NS.Should().Be(TestHelper.Namespace);
            token.Value.DB.Should().Be(TestHelper.Database);

            var invalidateResponse = await db.Invalidate();
            TestHelper.AssertOk(invalidateResponse);

            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Query(tokenSql, null));
        }
    );

    [Fact]
    public async Task SignInDatabaseUserErrorTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var user = "DatabaseUser";
            var password = "TestPassword";

            var signinObject = new DatabaseAuth(user, password, TestHelper.Namespace, TestHelper.Database);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertError(signinResponse);
        }
    );
    
#endregion Database User Auth

#region Scoped User Auth

    [Fact]
    public async Task SignUpAndSignInScopedUserTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var email = "TestUser@example.com";
            var password = "TestPassword";
            var scope = "account";

            string sql = $"DEFINE SCOPE {scope}\n"
              + "   SIGNIN ( SELECT * FROM user WHERE email = $user AND crypto::argon2::compare(password, $pass) )\n"
              + "   SIGNUP ( CREATE user SET email = $user, password = crypto::argon2::generate($pass) )\n"
              + ";"
              + ""
              + "DEFINE TABLE user SCHEMALESS\n"
              + "  PERMISSIONS\n"
              + "    FOR select, update WHERE id = $auth.id,\n    FOR create, delete NONE;"
              + ";";
            var queryResponse = await db.Query(sql, null);
            TestHelper.AssertOk(queryResponse);

            var signupObject = new ScopeAuth(email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signupResponse = await db.Signup(signupObject);
            TestHelper.AssertOk(signupResponse);
            ResultValue signupResult = signupResponse.FirstValue();
            string? signupJwt = signupResult.GetObject<string>();
            signupJwt.Should().NotBeNullOrEmpty();

            var signinObject = new ScopeAuth(email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertOk(signinResponse);
            ResultValue result = signinResponse.FirstValue();
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();

            var authenticateResponse = await db.Authenticate(signinJwt!);
            TestHelper.AssertOk(authenticateResponse);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            TestHelper.AssertOk(tokenQueryResponse);
            ResultValue tokenQueryResult = tokenQueryResponse.FirstValue();
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.NS.Should().Be(TestHelper.Namespace);
            token.Value.DB.Should().Be(TestHelper.Database);
            token.Value.SC.Should().Be(scope);

            string authSql = $"SELECT * FROM $auth;"; // This query required permisions to be set on the user table
            var authQueryResponse = await db.Query(authSql, null);
            TestHelper.AssertOk(authQueryResponse);
            ResultValue authQueryResult = authQueryResponse.FirstValue();
            User? authUser = authQueryResult.GetObject<User>();
            authUser.Should().NotBeNull();
            authUser.Value.id.Should().Be(token.Value.ID);
            authUser.Value.email.Should().Be(email);

            var infoResponse = await db.Info();
            TestHelper.AssertOk(infoResponse);
            ResultValue infoResult = infoResponse.FirstValue();
            User? infoUser = infoResult.GetObject<User>();
            infoUser.Should().BeEquivalentTo(authUser);

            var invalidateResponse = await db.Invalidate();
            TestHelper.AssertOk(invalidateResponse);

            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Query(tokenSql, null));
            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Info());
        }
    );

    [Fact]
    public async Task SignUpAndSignInScopedUserWithDefinedIdTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var email = "TestUser@example.com";
            var password = "TestPassword";
            var scope = "account";
            var id = "user:123";

            string sql = $"DEFINE SCOPE {scope}\n"
              + "   SIGNIN ( SELECT * FROM user WHERE email = $user AND crypto::argon2::compare(password, $pass) )\n"
              + "   SIGNUP ( CREATE $id SET email = $user, password = crypto::argon2::generate($pass) )\n"
              + ";"
              + ""
              + "DEFINE TABLE user SCHEMALESS\n"
              + "  PERMISSIONS\n"
              + "    FOR select, update WHERE id = $auth.id,\n    FOR create, delete NONE;"
              + ";";
            var queryResponse = await db.Query(sql, null);
            TestHelper.AssertOk(queryResponse);

            var signupObject = new IdScopeAuth(id, email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signupResponse = await db.Signup(signupObject);
            TestHelper.AssertOk(signupResponse);
            ResultValue signupResult = signupResponse.FirstValue();
            string? signupJwt = signupResult.GetObject<string>();
            signupJwt.Should().NotBeNullOrEmpty();

            var signinObject = new ScopeAuth(email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertOk(signinResponse);
            ResultValue result = signinResponse.FirstValue();
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();

            var authenticateResponse = await db.Authenticate(signinJwt!);
            TestHelper.AssertOk(authenticateResponse);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            TestHelper.AssertOk(tokenQueryResponse);
            ResultValue tokenQueryResult = tokenQueryResponse.FirstValue();
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.ID.Should().Be(id);
            token.Value.NS.Should().Be(TestHelper.Namespace);
            token.Value.DB.Should().Be(TestHelper.Database);
            token.Value.SC.Should().Be(scope);

            string authSql = $"SELECT * FROM $auth;"; // This query required permisions to be set on the user table
            var authQueryResponse = await db.Query(authSql, null);
            TestHelper.AssertOk(authQueryResponse);
            ResultValue authQueryResult = authQueryResponse.FirstValue();
            User? authUser = authQueryResult.GetObject<User>();
            authUser.Should().NotBeNull();
            authUser.Value.id.Should().Be(token.Value.ID);
            authUser.Value.email.Should().Be(email);

            var infoResponse = await db.Info();
            TestHelper.AssertOk(infoResponse);
            ResultValue infoResult = infoResponse.FirstValue();
            User? infoUser = infoResult.GetObject<User>();
            infoUser.Should().BeEquivalentTo(authUser);

            var invalidateResponse = await db.Invalidate();
            TestHelper.AssertOk(invalidateResponse);

            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Query(tokenSql, null));
            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Info());
        }
    );


    [Fact]
    public async Task SignUpScopedUserThenOpenConnectionWithJwtTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var email = "TestUser@example.com";
            var password = "TestPassword";
            var scope = "account";

            string sql = $"DEFINE SCOPE {scope}\n"
              + "   SIGNIN ( SELECT * FROM user WHERE email = $user AND crypto::argon2::compare(password, $pass) )\n"
              + "   SIGNUP ( CREATE user SET email = $user, password = crypto::argon2::generate($pass) )\n"
              + ";"
              + ""
              + "DEFINE TABLE user SCHEMALESS\n"
              + "  PERMISSIONS\n"
              + "    FOR select, update WHERE id = $auth.id,\n    FOR create, delete NONE;"
              + ";";
            var queryResponse = await db.Query(sql, null);
            TestHelper.AssertOk(queryResponse);

            var signupObject = new ScopeAuth(email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signupResponse = await db.Signup(signupObject);
            TestHelper.AssertOk(signupResponse);
            ResultValue signupResult = signupResponse.FirstValue();
            string? signupJwt = signupResult.GetObject<string>();
            signupJwt.Should().NotBeNullOrEmpty();

            var config = TestHelper.Default;
            config.Username = null;
            config.Password = null;
            config.JsonWebToken = signupJwt;
            await db.Close();
            db.Dispose();
            db = new();
            await db.Open(config);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            TestHelper.AssertOk(tokenQueryResponse);
            ResultValue tokenQueryResult = tokenQueryResponse.FirstValue();
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.NS.Should().Be(TestHelper.Namespace);
            token.Value.DB.Should().Be(TestHelper.Database);
            token.Value.SC.Should().Be(scope);

            string authSql = $"SELECT * FROM $auth;"; // This query required permisions to be set on the user table
            var authQueryResponse = await db.Query(authSql, null);
            TestHelper.AssertOk(authQueryResponse);
            ResultValue authQueryResult = authQueryResponse.FirstValue();
            User? authUser = authQueryResult.GetObject<User>();
            authUser.Should().NotBeNull();
            authUser.Value.id.Should().Be(token.Value.ID);
            authUser.Value.email.Should().Be(email);

            var infoResponse = await db.Info();
            TestHelper.AssertOk(infoResponse);
            ResultValue infoResult = infoResponse.FirstValue();
            User? infoUser = infoResult.GetObject<User>();
            infoUser.Should().BeEquivalentTo(authUser);

            var invalidateResponse = await db.Invalidate();
            TestHelper.AssertOk(invalidateResponse);

            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Query(tokenSql, null));
            await Assert.ThrowsAsync<InvalidOperationException>(async ()=> await db.Info());
        }
    );

    [Fact]
    public async Task SignInScopedUserErrorTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var email = "TestUser@example.com";
            var password = "TestPassword";
            var scope = "account";

            var signinObject = new ScopeAuth(email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signinResponse = await db.Signin(signinObject);
            TestHelper.AssertError(signinResponse);
        }
    );

#endregion Scoped User Auth
}
