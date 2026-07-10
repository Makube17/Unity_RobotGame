# Network Recovery Notes

## Summary

Unity could not reach the AI server because the Ubuntu machine dropped off the network/Tailscale path.
The Unity project and AI endpoint code were not the main cause in this case.

## What Happened

- Windows could not connect to `100.77.168.49:5000`.
- `tailscale status` on Windows showed the Ubuntu machine as `offline`.
- Ubuntu's wired connection was detected, but the network route was not correct at first.
- In particular, Ubuntu did not initially have a valid `default via ...` route, so it could not reach the internet.
- Because Ubuntu could not reach the internet, Tailscale could not recover.
- As a result, Unity could not communicate with the AI server.

## Cause

The immediate cause was that the Ubuntu PC lost usable internet connectivity on its wired network connection.

The likely underlying cause is that the Ubuntu wired connection profile had switched to, or was affected by, the robot-direct/research network configuration.
That kind of configuration may use a fixed IP and may not have a default gateway, which is correct for direct robot-controller communication but not suitable for internet/Tailscale communication.

## Recovery Steps That Worked

1. Confirmed that the issue was outside Unity:

   ```bash
   tailscale status
   ip route
   ```

2. Confirmed that Ubuntu had wired network detection but not a usable internet route.

3. Kept the existing robot/research wired profile instead of deleting it.

4. Created a separate internet/DHCP wired profile:

   ```bash
   sudo nmcli connection add type ethernet ifname enp61s0
   sudo nmcli connection modify ethernet-enp61s0 connection.id wired-internet
   ```

5. Switched from the robot/research profile to the internet profile:

   ```bash
   sudo nmcli connection down wired-robot
   sudo nmcli connection up wired-internet
   ```

6. Confirmed that Ubuntu had a default route:

   ```bash
   ip route
   ```

   Expected:

   ```text
   default via <router-ip> dev enp61s0
   ```

7. Confirmed HTTP internet access:

   ```bash
   curl -I http://example.com
   curl -I http://1.1.1.1
   ```

   Success means a response starting with `HTTP/` appears.
   `HTTP/1.1 200`, `HTTP/1.1 301`, and `HTTP/1.1 403` are all acceptable for this check.

8. Confirmed Tailscale was online:

   ```bash
   tailscale status
   ```

   `linux -` and `windows -` mean the devices are online with no error message.

9. Confirmed the Tailscale IP:

   ```bash
   tailscale ip -4
   ```

10. Confirmed Windows could reach the AI server again:

    ```powershell
    Test-NetConnection 100.77.168.49 -Port 5000
    ```

    Expected:

    ```text
    TcpTestSucceeded : True
    ```

## Important Notes About Ping

`ping` may fail even when HTTP communication works.

In this recovery, `curl` succeeded even though ping checks had failed earlier.
This means ICMP ping may have been blocked somewhere, while normal HTTP traffic was still allowed.

For checking internet access, `curl -I http://example.com` was more useful than ping.

## Switching Profiles

Use `wired-internet` for Unity/Tailscale/AI communication through the router:

```bash
sudo nmcli connection down wired-robot
sudo nmcli connection up wired-internet
```

Use `wired-robot` for research robot-controller direct connection:

```bash
sudo nmcli connection down wired-internet
sudo nmcli connection up wired-robot
```

Check which profile is active:

```bash
nmcli connection show
```

The active connection appears highlighted/green in the terminal.

## Cautions For Research Use

- Do not delete `wired-robot`.
- Do not change `wired-robot` to DHCP unless the robot setup explicitly requires it.
- The robot-direct profile may intentionally use a fixed IP and no default gateway.
- A profile with no default gateway can be correct for robot control but cannot reach the internet.
- Use `wired-internet` only when the Ubuntu PC is connected to the normal router/network.
- Use `wired-robot` when connecting directly to the robot controller.

## Quick Checklist For Next Time

Ubuntu:

```bash
nmcli connection show
ip route
curl -I http://example.com
tailscale status
tailscale ip -4
ss -ltnp | grep 5000
```

Windows:

```powershell
tailscale status
Test-NetConnection 100.77.168.49 -Port 5000
```

Unity:

- Confirm `ImageSender` server URL is still `http://100.77.168.49:5000/predict`.
- If Windows `Test-NetConnection` succeeds but Unity fails, then check Unity URL, timeout, and AI server response.
