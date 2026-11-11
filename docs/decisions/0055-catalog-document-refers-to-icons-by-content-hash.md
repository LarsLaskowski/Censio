# 0055: The catalog document refers to each icon by the SHA-256 of its PNG and carries no image data

- **Status:** Accepted
- **Date:** 2026-11-11
- **Area:** game-catalog-format
- **Source:** Issue #26
- **Supersedes:** —

## Context

The server cannot read game files and gets names and icons from the Companion
([0023](0023-game-names-and-icons-extracted-locally.md)); the upload (#42) is to skip what the server already has. The
icons of one installation are PNG files of up to 256 MiB together ([`game-catalog-icons.md`](../areas/game-catalog-icons.md)),
the names some hundred KiB. Several entries share one image. Issue #26 asks for "icon references".

## Options considered

1. **PNG data inside the document (Base64)** — one self-contained document; a third more bytes, every upload resends
   every image, and the document of names grows to hundreds of MiB.
2. **A reference by position (an index into an icon list of the document)** — small; the index changes with every
   extraction, so a receiver cannot tell an image it already holds.
3. **A reference by content, the SHA-256 of the PNG in lowercase hex** — the same image gives the same reference in
   every catalog and installation, a receiver can check an image against it and skip one it has; the images travel
   separately (#27, #42).
4. **The game's icon file name as reference** — readable; a game path is no identity of the converted image (a patch
   archive can change the image under the same name).

## Decision

Option 3: an entry with an icon carries the SHA-256 of its PNG data as 64 lowercase hex digits; the document holds no
image data. The rules are in [`game-catalog-format.md`](../areas/game-catalog-format.md).

## Consequences

- The upload and the server's store of images are #27 and #42; they can key images by the reference.
- The hash is an identity, not a security control: nothing in this feature trusts an image because of it.
- Embedding images later is a new schema version ([0056](0056-catalog-document-read-strictly-by-exact-schema-version.md)).
