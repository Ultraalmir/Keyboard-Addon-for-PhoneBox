# Security policy

## Supported version

Security fixes are applied to the latest release of PhoneBox Keyboard Add-on.

## Reporting a vulnerability

Please do not publish exploitable details in a public issue. Contact the repository owner privately through GitHub's **Report a vulnerability** feature, including reproduction steps and affected version.

## Security design

- The settings server binds to IPv4 loopback only (`127.0.0.1`).
- Requests must use the expected `Host`; browser requests with an `Origin` must match the local settings origin.
- The PhoneBox destination is restricted to a loopback HTTP URL. Its port is configurable, but the client only calls fixed API paths and never follows redirects.
- The application does not accept remote configuration commands or expose a LAN listener.
- The process runs as the current user and does not request administrator privileges.
- The loopback server is intended for the local user's settings page, not as authentication against other processes already running on the same Windows account. It has no bearer token; do not run untrusted local software alongside it.
