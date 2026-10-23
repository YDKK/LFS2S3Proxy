# LFS2S3Proxy
Git LFS Proxy (use Amazon S3 as datastore)  

<img src="https://cdn-ak.f.st-hatena.com/images/fotolife/Y/YDKK/20171207/20171207012533.png" width=500>

## Usage
Install the .NET 10 SDK. From the `LFS2S3Proxy` directory, copy `config.example.json` to `config.json` and edit the settings:

```sh
cd LFS2S3Proxy
cp config.example.json config.json
```

- `s3bucketName`: S3 bucket name
- `token`: A non-empty token included as a single URL path segment when accessing (case-sensitive).
- `listen`: HTTP server listen URL

Run the console application from that directory:

```
dotnet run
```

Press Ctrl+C to stop it.

To configure Git repository to use this proxy, run this command:
```sh
git config -f .lfsconfig lfs.url https://your.host.name/{token}/{repositoryName}
```

Enjoy!
