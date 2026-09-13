namespace ChronicleOfHeros.Web.Components.Pages;

internal static class AuthenticationJourneyPrototypeLogic
{
    private const string PlayerHomePath = "/characters";
    private const string OperatorAdministrationPath = "/operator/players";

    internal static AuthenticationJourneyState InitialState { get; } = new(
        JourneyScreen.SignIn,
        BrowserSessionState.None,
        JourneyRole.None,
        null,
        null,
        null,
        false,
        "No browser session is active.");

    internal static AuthenticationJourneyState Reduce(
        AuthenticationJourneyState state,
        AuthenticationJourneyAction action)
    {
        return action switch
        {
            AuthenticationJourneyAction.SignInAsRestrictedPlayer => state with
            {
                Screen = JourneyScreen.MandatoryPasswordReplacement,
                BrowserSession = BrowserSessionState.Restricted,
                Role = JourneyRole.Player,
                PendingReturnPath = null,
                TemporaryCredential = null,
                CredentialDisclosurePurpose = null,
                IsTemporaryCredentialAwaitingAcknowledgement = false,
                LastChange = "The Player has a restricted session and must replace the temporary credential.",
            },
            AuthenticationJourneyAction.SignInAsRestrictedOperator => state with
            {
                Screen = JourneyScreen.MandatoryPasswordReplacement,
                BrowserSession = BrowserSessionState.Restricted,
                Role = JourneyRole.Operator,
                PendingReturnPath = null,
                TemporaryCredential = null,
                CredentialDisclosurePurpose = null,
                IsTemporaryCredentialAwaitingAcknowledgement = false,
                LastChange = "The Operator has a restricted session and must replace the temporary credential.",
            },
            AuthenticationJourneyAction.SignInAsPlayer => ActivePlayerState(state),
            AuthenticationJourneyAction.SignInAsOperator => ActiveOperatorState(state),
            AuthenticationJourneyAction.NavigateToPlayerHome => NavigateToPlayerHome(state),
            AuthenticationJourneyAction.NavigateToOperatorAdministration => NavigateToOperatorAdministration(state),
            AuthenticationJourneyAction.CompleteMandatoryPasswordReplacement => CompleteMandatoryPasswordReplacement(state),
            AuthenticationJourneyAction.LoseSession => EndSession(state),
            AuthenticationJourneyAction.EnrollPlayer => StartPlayerEnrollment(state),
            AuthenticationJourneyAction.ResetPlayerPassword => ResetPlayerPassword(state),
            AuthenticationJourneyAction.AcknowledgeTemporaryCredential => AcknowledgeTemporaryCredential(state),
            AuthenticationJourneyAction.ReturnToSignIn => InitialState with
            {
                LastChange = "The browser returned to the explicit sign-in route.",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
    }

    private static AuthenticationJourneyState ActivePlayerState(AuthenticationJourneyState state)
    {
        return state with
        {
            Screen = JourneyScreen.PlayerHome,
            BrowserSession = BrowserSessionState.Active,
            Role = JourneyRole.Player,
            PendingReturnPath = null,
            TemporaryCredential = null,
            CredentialDisclosurePurpose = null,
            IsTemporaryCredentialAwaitingAcknowledgement = false,
            LastChange = "The Player has an active browser session and can manage their character data.",
        };
    }

    private static AuthenticationJourneyState ActiveOperatorState(AuthenticationJourneyState state)
    {
        return state with
        {
            Screen = JourneyScreen.OperatorAdministration,
            BrowserSession = BrowserSessionState.Active,
            Role = JourneyRole.Operator,
            PendingReturnPath = null,
            TemporaryCredential = null,
            CredentialDisclosurePurpose = null,
            IsTemporaryCredentialAwaitingAcknowledgement = false,
            LastChange = "The Operator has an active browser session and can administer Player accounts.",
        };
    }

    private static AuthenticationJourneyState NavigateToPlayerHome(AuthenticationJourneyState state)
    {
        return state.BrowserSession == BrowserSessionState.Active
            ? state with
            {
                Screen = JourneyScreen.PlayerHome,
                LastChange = "The active session can open the Player's character-management area.",
            }
            : state.BrowserSession == BrowserSessionState.Restricted
                ? state with
                {
                    Screen = JourneyScreen.MandatoryPasswordReplacement,
                    PendingReturnPath = PlayerHomePath,
                    LastChange = "Restricted sessions are sent to mandatory password replacement before character management.",
                }
                : state with
                {
                    Screen = JourneyScreen.SignIn,
                    PendingReturnPath = PlayerHomePath,
                    LastChange = "No active session exists, so protected navigation returns to sign-in with a local continuation.",
                };
    }

    private static AuthenticationJourneyState NavigateToOperatorAdministration(AuthenticationJourneyState state)
    {
        return state.BrowserSession == BrowserSessionState.Active && state.Role == JourneyRole.Operator
            ? state with
            {
                Screen = JourneyScreen.OperatorAdministration,
                LastChange = "The active Operator session can open Player administration.",
            }
            : state.BrowserSession == BrowserSessionState.Restricted
                ? state with
                {
                    Screen = JourneyScreen.MandatoryPasswordReplacement,
                    PendingReturnPath = OperatorAdministrationPath,
                    LastChange = "Restricted sessions are sent to mandatory password replacement before Player administration.",
                }
                : state.BrowserSession == BrowserSessionState.Active
                    ? state with
                    {
                        Screen = JourneyScreen.AccessDenied,
                        LastChange = "An active Player session cannot access Operator administration.",
                    }
                    : state with
                    {
                        Screen = JourneyScreen.SignIn,
                        PendingReturnPath = OperatorAdministrationPath,
                        LastChange = "No active session exists, so Operator administration returns to sign-in with a local continuation.",
                    };
    }

    private static AuthenticationJourneyState CompleteMandatoryPasswordReplacement(AuthenticationJourneyState state)
    {
        return state.BrowserSession != BrowserSessionState.Restricted
            ? Reject(state, "Password replacement is only available to a restricted session.")
            : state with
            {
                Screen = state.PendingReturnPath == OperatorAdministrationPath && state.Role == JourneyRole.Operator
                    ? JourneyScreen.OperatorAdministration
                    : JourneyScreen.PlayerHome,
                BrowserSession = BrowserSessionState.Active,
                PendingReturnPath = null,
                LastChange = "Password replacement activated the browser session and resumed the allowed destination.",
            };
    }

    private static AuthenticationJourneyState EndSession(AuthenticationJourneyState state)
    {
        string? continuation = state.PendingReturnPath
            ?? (state.Screen == JourneyScreen.OperatorAdministration
                ? OperatorAdministrationPath
                : state.Screen == JourneyScreen.PlayerHome ? PlayerHomePath : null);

        return state with
        {
            Screen = JourneyScreen.SessionEnded,
            BrowserSession = BrowserSessionState.Ended,
            Role = JourneyRole.None,
            PendingReturnPath = continuation,
            TemporaryCredential = null,
            CredentialDisclosurePurpose = null,
            IsTemporaryCredentialAwaitingAcknowledgement = false,
            LastChange = "The browser session is no longer valid. Protected actions require a fresh sign-in.",
        };
    }

    private static AuthenticationJourneyState StartPlayerEnrollment(AuthenticationJourneyState state)
    {
        return state.BrowserSession != BrowserSessionState.Active || state.Role != JourneyRole.Operator
            ? Reject(state, "Only an active Operator session can enroll a Player.")
            : state with
            {
                Screen = JourneyScreen.TemporaryCredentialDisclosure,
                TemporaryCredential = "maple-bridge-71",
                CredentialDisclosurePurpose = CredentialDisclosurePurpose.PlayerEnrollment,
                IsTemporaryCredentialAwaitingAcknowledgement = true,
                LastChange = "A temporary credential is visible once for direct delivery outside the application.",
            };
    }

    private static AuthenticationJourneyState ResetPlayerPassword(AuthenticationJourneyState state)
    {
        return state.BrowserSession != BrowserSessionState.Active || state.Role != JourneyRole.Operator
            ? Reject(state, "Only an active Operator session can reset a Player's password.")
            : state with
            {
                Screen = JourneyScreen.TemporaryCredentialDisclosure,
                TemporaryCredential = "river-lantern-49",
                CredentialDisclosurePurpose = CredentialDisclosurePurpose.PasswordReset,
                IsTemporaryCredentialAwaitingAcknowledgement = true,
                LastChange = "A reset password is visible once for direct delivery outside the application. The Player must replace it after signing in.",
            };
    }

    private static AuthenticationJourneyState AcknowledgeTemporaryCredential(AuthenticationJourneyState state)
    {
        return !state.IsTemporaryCredentialAwaitingAcknowledgement
            ? Reject(state, "There is no temporary credential awaiting acknowledgement.")
            : state with
            {
                Screen = JourneyScreen.OperatorAdministration,
                TemporaryCredential = null,
                CredentialDisclosurePurpose = null,
                IsTemporaryCredentialAwaitingAcknowledgement = false,
                LastChange = "The Operator acknowledged the temporary credential disclosure and returned to Player administration.",
            };
    }

    private static AuthenticationJourneyState Reject(AuthenticationJourneyState state, string reason)
    {
        return state with
        {
            Screen = JourneyScreen.AccessDenied,
            LastChange = reason,
        };
    }
}

internal sealed record AuthenticationJourneyState(
    JourneyScreen Screen,
    BrowserSessionState BrowserSession,
    JourneyRole Role,
    string? PendingReturnPath,
    string? TemporaryCredential,
    CredentialDisclosurePurpose? CredentialDisclosurePurpose,
    bool IsTemporaryCredentialAwaitingAcknowledgement,
    string LastChange);

internal enum AuthenticationJourneyAction
{
    SignInAsRestrictedPlayer,
    SignInAsRestrictedOperator,
    SignInAsPlayer,
    SignInAsOperator,
    NavigateToPlayerHome,
    NavigateToOperatorAdministration,
    CompleteMandatoryPasswordReplacement,
    LoseSession,
    EnrollPlayer,
    ResetPlayerPassword,
    AcknowledgeTemporaryCredential,
    ReturnToSignIn,
}

internal enum JourneyScreen
{
    SignIn,
    MandatoryPasswordReplacement,
    PlayerHome,
    OperatorAdministration,
    TemporaryCredentialDisclosure,
    SessionEnded,
    AccessDenied,
}

internal enum BrowserSessionState
{
    None,
    Restricted,
    Active,
    Ended,
}

internal enum JourneyRole
{
    None,
    Player,
    Operator,
}

internal enum CredentialDisclosurePurpose
{
    PlayerEnrollment,
    PasswordReset,
}