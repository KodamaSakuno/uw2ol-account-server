import { useCallback, useState } from "react"
import { Web3Provider } from "@ethersproject/providers"
import { auth, getNonce, login, register } from "./services"

enum State {
  Login,
  RegisterOrConfirm,
  Success,
}

let accessToken: string | null = null;

function App() {
  const [state, setState] = useState(State.Login);
  const [loading, setLoading] = useState(false);
  const [name, setName] = useState<string>("");

  const onAuth = useCallback(async () => {
    if (!(window as any).ethereum) {
      window.alert("Please install MetaMask first.");
      return;
    }

    const session = new URLSearchParams(window.location.search).get("session");

    if (!session) {
      window.alert("Missing session");
      return;
    }

    setLoading(true);

    try {
      await (window as any).ethereum.request({ method: "eth_requestAccounts" });

      const provider = new Web3Provider((window as any).ethereum);
      const signer = provider.getSigner();

      const address = await signer.getAddress();
      const nonce = await getNonce(address);
      const signature = await signer.signMessage(`I'm signing my one-time nonce: ${nonce}`);

      const authResponse = await auth(address, signature, session);

      accessToken = authResponse.accessToken;
      setName(authResponse.name ?? "");
      setState(State.RegisterOrConfirm);
    } finally {
      setLoading(false);
    }
  }, [setLoading, setName, setState]);

  const onLogin = useCallback(async () => {
    try {
      setLoading(true);
      await login(accessToken!);
      setState(State.Success);
    } finally {
      setLoading(false);
    }
  }, [setLoading, setState]);

  const [inputName, setInputName] = useState("");

  const onInputNameChange = useCallback((e: React.FormEvent<HTMLInputElement>) => {
    setInputName(e.currentTarget.value);
  }, [setInputName]);

  const onRegister = useCallback(async () => {
    try {
      setLoading(true);
      await register(accessToken!, inputName);
      setState(State.Success);
    } finally {
      setLoading(false);
    }
  }, [setLoading, setState, inputName]);

  if (state === State.Login) {
    return (
      <div>
        <button onClick={onAuth} disabled={loading}>
          {loading ? "Loading..." : "Login with Metamask"}
        </button>
      </div>
    );
  }

  if (state === State.RegisterOrConfirm) {
    if (name.length > 0) {
      return (
        <div>
          <span>Your character name: {name}</span>
          <button onClick={onLogin} disabled={loading}>
            {loading ? "Loading..." : "Confirm"}
          </button>
        </div>
      );
    }

    return (
      <div>
        <span>Please input your name:</span>
        <input type="text" minLength={3} value={inputName} onChange={onInputNameChange} />
          <button onClick={onRegister} disabled={loading}>
            {loading ? "Loading..." : "Confirm"}
          </button>
      </div>
    );
  }

  return (
    <div>
      Success
    </div>
  );
}

export default App;
