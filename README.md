# AssetExplorer

AssetExplorer is a Unity client implementation of AssetDatabase (https://assetdatabase.natsuneko.cat).  
As an editor extension, you can take over Unity's workflow and search for assets.

## Features

### Reverse Lookup by Assets

You can search for assets that **missing** references the specified asset.

### Reverse Lookup by GUID

You can search for assets by the specified GUID.

### Verify Assets

> ![NOTE]
> This feature requires an API token. API token issuance requires authentication by a third-party IdP.

Verifies that the registered asset information is correct and submits the results.  
This makes it possible to improve the reliability of the registered data.

### Contribute Assets

> ![NOTE]
> This feature requires an API token. API token issuance requires authentication by a third-party IdP.

Register a new asset information.

## How it works

AssetExplorer uses the AssetDatabase API to search for assets.  
[AssetDatabase](https://assetdatabase.natsuneko.cat) is a web service that provides a database of assets that can be used in Unity (developed by me).  
AssetExplorer uses the API provided by AssetDatabase to search, verify, and register for assets.

## License

MIT by [@6jz](https://twitter.com/6jz)
