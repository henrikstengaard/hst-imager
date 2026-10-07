# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

Windows releases of Hst Imager are signed using SignPath. Only release binaries built from the source code in this repository by the GitHub Actions build pipeline are signed.

## Team roles

- Committers and reviewers: [Contributors](https://github.com/henrikstengaard/hst-imager/graphs/contributors)
- Approvers: [Henrik Nørfjand Stengaard](https://github.com/henrikstengaard)

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

When formatting a physical drive or image file with the PFS3 file system, the user is asked before Hst Imager downloads `pfs3aio.lha` from [Aminet](https://aminet.net/disk/misc/pfs3aio.lha). Hst Imager Gui asks in the format confirmation and Hst Imager Console asks with a prompt, unless the user provides a path to a pfs3aio file. No user information is sent as part of this download.
