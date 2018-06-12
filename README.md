# LFS2S3Proxy
Git LFS Proxy (use Amazon S3 as datastore)  
https://ydkk.hateblo.jp/entry/2017/12/07/120000

## Usage
Edit `config.example.json` and save it as `config.json`.

`s3bucketName`: S3 bucket name  
`token`: The token string which needs to include in the URL when accessing.  
`listen`: HTTP server listen URL  

You can install as Windows service.
```
> LFS2S3Proxy.exe install
```

Enjoy!