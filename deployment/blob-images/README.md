# Azure Blob image package

This folder contains the deployment-ready player image assets for Football Mayhem.

- Upload every image file while preserving its relative path.
- Use a private or public blob container named `player-images` (the deployment configuration will decide the access model).
- The `manifest.json` file lists each blob path, byte size, and SHA-256 checksum for deployment verification.
- Do **not** upload the original `*_rank_list_*.txt` source notes from the local image library.

The directory structure is intentional. For example, `CAM/1_Diego_Maradona.jpg` must remain the blob path `CAM/1_Diego_Maradona.jpg`, producing the final URL:

`https://<storage-account>.blob.core.windows.net/player-images/CAM/1_Diego_Maradona.jpg`

Current package: 216 player images, approximately 37.1 MiB.
