using SurrealDB.Common;
// ReSharper disable All
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
    
    [Fact]
    public async Task SignInRootAuthTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var signinObject = new RootAuth(TestHelper.User, TestHelper.Pass);
            var response = await db.Signin(signinObject);
            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().BeNullOrEmpty();
        }
    );

    [Fact]
    public async Task SignInNamespaceUserTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var user = "DatabaseUser";
            var password = "TestPassword";

            string sql = $"DEFINE LOGIN {user} ON NAMESPACE PASSWORD '{password}';";
            var queryResponse = await db.Query(sql, null);
            Assert.NotNull(queryResponse);
            TestHelper.AssertOk(queryResponse);

            var signinObject = new NamespaceAuth(user, password, TestHelper.Namespace);
            var signinResponse = await db.Signin(signinObject);
            Assert.NotNull(signinResponse);
            TestHelper.AssertOk(signinResponse);
            Assert.True(signinResponse.TryGetResult(out Result result));
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();
            
            var authenticateResponse = await db.Authenticate(signinJwt);
            Assert.NotNull(authenticateResponse);
            TestHelper.AssertOk(authenticateResponse);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            Assert.NotNull(tokenQueryResponse);
            TestHelper.AssertOk(tokenQueryResponse);
            Assert.True(tokenQueryResponse.TryGetResult(out Result tokenQueryResult));
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.ID.Should().Be(user);
            token.Value.NS.Should().Be(TestHelper.Namespace);
        }
    );

    [Fact]
    public async Task SignInDatabaseUserTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var user = "DatabaseUser";
            var password = "TestPassword";

            string sql = $"DEFINE LOGIN {user} ON DATABASE PASSWORD '{password}';";
            var queryResponse = await db.Query(sql, null);
            Assert.NotNull(queryResponse);
            TestHelper.AssertOk(queryResponse);

            var signinObject = new DatabaseAuth(user, password, TestHelper.Namespace, TestHelper.Database);
            var signinResponse = await db.Signin(signinObject);
            Assert.NotNull(signinResponse);
            TestHelper.AssertOk(signinResponse);
            Assert.True(signinResponse.TryGetResult(out Result result));
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();
            
            var authenticateResponse = await db.Authenticate(signinJwt);
            Assert.NotNull(authenticateResponse);
            TestHelper.AssertOk(authenticateResponse);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            Assert.NotNull(tokenQueryResponse);
            TestHelper.AssertOk(tokenQueryResponse);
            Assert.True(tokenQueryResponse.TryGetResult(out Result tokenQueryResult));
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.ID.Should().Be(user);
            token.Value.NS.Should().Be(TestHelper.Namespace);
            token.Value.DB.Should().Be(TestHelper.Database);
        }
    );
    
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
            Assert.NotNull(queryResponse);
            TestHelper.AssertOk(queryResponse);
            
            var signupObject = new ScopeAuth(email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signupResponse = await db.Signup(signupObject);
            Assert.NotNull(signupResponse);
            TestHelper.AssertOk(signupResponse);
            Assert.True(signupResponse.TryGetResult(out Result signupResult));
            string? signupJwt = signupResult.GetObject<string>();
            signupJwt.Should().NotBeNullOrEmpty();

            var signinObject = new ScopeAuth(email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signinResponse = await db.Signin(signinObject);
            Assert.NotNull(signinResponse);
            TestHelper.AssertOk(signinResponse);
            Assert.True(signinResponse.TryGetResult(out Result result));
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();
            
            var authenticateResponse = await db.Authenticate(signinJwt);
            Assert.NotNull(authenticateResponse);
            TestHelper.AssertOk(authenticateResponse);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            Assert.NotNull(tokenQueryResponse);
            TestHelper.AssertOk(tokenQueryResponse);
            Assert.True(tokenQueryResponse.TryGetResult(out Result tokenQueryResult));
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.NS.Should().Be(TestHelper.Namespace);
            token.Value.DB.Should().Be(TestHelper.Database);
            token.Value.SC.Should().Be(scope);

            string authSql = $"SELECT id, email FROM $auth;"; // This query required permisions to be set on the user table
            var authQueryResponse = await db.Query(authSql, null);
            Assert.NotNull(authQueryResponse);
            TestHelper.AssertOk(authQueryResponse);
            Assert.True(authQueryResponse.TryGetResult(out Result authQueryResult));
            User? user = authQueryResult.GetObject<User>();
            user.Should().NotBeNull();
            user.Value.id.Should().Be(token.Value.ID);
            user.Value.email.Should().Be(email);
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
            Assert.NotNull(queryResponse);
            TestHelper.AssertOk(queryResponse);
            
            var signupObject = new IdScopeAuth(id, email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signupResponse = await db.Signup(signupObject);
            Assert.NotNull(signupResponse);
            TestHelper.AssertOk(signupResponse);
            Assert.True(signupResponse.TryGetResult(out Result signupResult));
            string? signupJwt = signupResult.GetObject<string>();
            signupJwt.Should().NotBeNullOrEmpty();

            var signinObject = new ScopeAuth(email, password, TestHelper.Namespace, TestHelper.Database, scope);
            var signinResponse = await db.Signin(signinObject);
            Assert.NotNull(signinResponse);
            TestHelper.AssertOk(signinResponse);
            Assert.True(signinResponse.TryGetResult(out Result result)); 
            string? signinJwt = result.GetObject<string>();
            signinJwt.Should().NotBeNullOrEmpty();
            
            var authenticateResponse = await db.Authenticate(signinJwt);
            Assert.NotNull(authenticateResponse);
            TestHelper.AssertOk(authenticateResponse);

            string tokenSql = $"SELECT * FROM $token;";
            var tokenQueryResponse = await db.Query(tokenSql, null);
            Assert.NotNull(tokenQueryResponse);
            TestHelper.AssertOk(tokenQueryResponse);
            Assert.True(tokenQueryResponse.TryGetResult(out Result tokenQueryResult));
            Token? token = tokenQueryResult.GetObject<Token>();
            token.Should().NotBeNull();
            token.Value.ID.Should().Be(id);
            token.Value.NS.Should().Be(TestHelper.Namespace);
            token.Value.DB.Should().Be(TestHelper.Database);
            token.Value.SC.Should().Be(scope);

            string authSql = $"SELECT id, email FROM $auth;"; // This query required permisions to be set on the user table
            var authQueryResponse = await db.Query(authSql, null);
            Assert.NotNull(authQueryResponse);
            TestHelper.AssertOk(authQueryResponse);
            Assert.True(authQueryResponse.TryGetResult(out Result authQueryResult));
            User? user = authQueryResult.GetObject<User>();
            user.Should().NotBeNull();
            user.Value.id.Should().Be(token.Value.ID);
            user.Value.email.Should().Be(email);
        }
    );
}
