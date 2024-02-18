namespace SipIntercept;

public interface IApp
{
    void Init();
    void ReopenApp();
    void OpenChat(string number);
    bool CheckNuberIsValid(string number);
    void CallCurrentContact();
    void EndCall();
    bool IsCallingScreen();
    bool IsCallDeclined();
    bool IsCallActive();
    bool IsRinging();
    void CallAgain();
    bool IsNoAnswer();
    bool IsContactScreen();
    void CancellCall();
}