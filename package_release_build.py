#!/usr/bin/env python3
# Packages a full release build that can be unzipped and you'll have your SS14 client or server.

import os
import shutil
import subprocess
import sys
import zipfile
import argparse

try:
    from colorama import init, Fore, Style
    init()

except ImportError:
    # Just give an empty string for everything, no colored logging.
    class ColorDummy(object):
        def __getattr__(self, name):
            return ""

    Fore = ColorDummy()
    Style = ColorDummy()


SHARED_IGNORED_RESOURCES = {
    "ss13model.7z",
    "ResourcePack.zip",
    "buildResourcePack.py",
    "CONTENT_GOES_HERE"
}
CLIENT_IGNORED_RESOURCES = {
    "Maps",
    "emotes.xml"
}
SERVER_IGNORED_RESOURCES = {
    "Textures",
    "Fonts"
}

GODOT = "/home/pjbriers/builds_shared/godot"


def main():
    parser = argparse.ArgumentParser(
        description="Packages the SS14 content repo for release on all platforms.")
    parser.add_argument("--platform",
                        "-p",
                        action="store",
                        choices=["windows", "mac", "linux"],
                        nargs="*",
                        help="Which platform to build for. If not provided, all platforms will be built")

    args = parser.parse_args()
    platforms = args.platform

    if not platforms:
        platforms = ["windows", "mac", "linux"]

    if os.path.exists("release"):
        print(Fore.BLUE+Style.DIM +
              "Cleaning old release packages (release/)..." + Style.RESET_ALL)
        shutil.rmtree("release")

    os.mkdir("release")

    if "windows" in platforms:
        wipe_bin()
        build_windows()

    if "linux" in platforms:
        wipe_bin()
        build_linux()

    if "mac" in platforms:
        wipe_bin()
        build_macos()


def wipe_bin():
    print(Fore.BLUE + Style.DIM +
          "Clearing old build artifacts (if any)..." + Style.RESET_ALL)
    if os.path.exists(os.path.join("engine", "bin")):
        shutil.rmtree(os.path.join("engine", "bin"))

    if os.path.exists("bin"):
        shutil.rmtree("bin")


def build_windows():
    # Run a full build.
    print(Fore.GREEN + "Building project for Windows x64..." + Style.RESET_ALL)
    subprocess.run(["msbuild",
                    "SpaceStation14Content.sln",
                    "/m",
                    "/p:Configuration=Release",
                    "/p:Platform=x64",
                    "/nologo",
                    "/v:m",
                    "/p:TargetOS=Windows",
                    "/t:Rebuild"
                    ], check=True)

    print(Fore.GREEN + "Packaging Windows x64 client..." + Style.RESET_ALL)
    bundle = os.path.join("bin", "win_app")
    shutil.copytree(os.path.join("BuildFiles", "Windows"),
                    bundle)

    os.makedirs(os.path.join(bundle, "bin", "Client"), exist_ok=True)

    _copytree(os.path.join("engine", "bin", "Client"),
              os.path.join(bundle, "bin", "Client"))

    copy_resources(os.path.join(
        bundle, "bin", "Client", "Resources"), server=False)

    os.makedirs(os.path.join(bundle, "SS14.Client.Godot"), exist_ok=True)

    _copytree(os.path.join("engine", "SS14.Client.Godot"),
              os.path.join(bundle, "SS14.Client.Godot"))

    package_zip(os.path.join("bin", "win_app"),
                os.path.join("release", "SS14.Client_Windows_x64.zip"))

    print(Fore.GREEN + "Packaging Windows x64 server..." + Style.RESET_ALL)
    copy_resources(os.path.join("engine", "bin",
                                "Server", "Resources"), server=True)
    package_zip(os.path.join("engine", "bin", "Server"),
                os.path.join("release", "SS14.Server_windows_x64.zip"))


def build_linux():
    print(Fore.GREEN + "Building project for Linux x64..." + Style.RESET_ALL)
    subprocess.run(["msbuild",
                    "SpaceStation14Content.sln",
                    "/m",
                    "/p:Configuration=Release",
                    "/p:Platform=x64",
                    "/nologo",
                    "/v:m",
                    "/p:TargetOS=Linux",
                    "/t:Rebuild"
                    ], check=True)

    # NOTE: Temporarily disabled because I can't test it.
    # Package client.
    #print(Fore.GREEN + "Packaging Linux x64 client..." + Style.RESET_ALL)
    # package_zip(os.path.join("bin", "Client"), os.path.join(
    #    "release", "SS14.Client_linux_x64.zip"))

    print(Fore.GREEN + "Packaging Linux x64 server..." + Style.RESET_ALL)
    copy_resources(os.path.join("engine", "bin",
                                "Server", "Resources"), server=True)
    package_zip(os.path.join("engine", "bin", "Server"), os.path.join(
        "release", "SS14.Server_linux_x64.zip"))


def build_macos():
    print(Fore.GREEN + "Building project for MacOS x64..." + Style.RESET_ALL)
    subprocess.run(["msbuild",
                    "SpaceStation14Content.sln",
                    "/m",
                    "/p:Configuration=Release",
                    "/p:Platform=x64",
                    "/nologo",
                    "/v:m",
                    "/p:TargetOS=MacOS",
                    "/t:Rebuild"
                    ], check=True)

    print(Fore.GREEN + "Packaging MacOS x64 client..." + Style.RESET_ALL)
    # Client has to go in an app bundle.
    subprocess.run(GODOT,
                   "--verbose",
                   "--export-debug",
                   "mac",
                   "../../release/mac_export.zip",
                   cwd="engine/SS14.Client.Godot",
                   check=True)

    _copytree(os.path.join("engine", "bin", "Client"),
              os.path.join(bundle, "Contents", "MacOS", "bin", "Client"))

    copy_resources(os.path.join(bundle, "Contents",
                                "MacOS", "bin", "Client", "Resources"), server=False)

    os.makedirs(os.path.join(bundle, "Contents", "MacOS",
                             "SS14.Client.Godot"), exist_ok=True)

    _copytree(os.path.join("engine", "SS14.Client.Godot"),
              os.path.join(bundle, "Contents", "MacOS", "SS14.Client.Godot"))

    package_zip(os.path.join("bin", "mac_app"),
                os.path.join("release", "SS14.Client_MacOS.zip"))

    print(Fore.GREEN + "Packaging MacOS x64 server..." + Style.RESET_ALL)
    copy_resources(os.path.join("engine", "bin",
                                "Server", "Resources"), server=True)

    package_zip(os.path.join("engine", "bin", "Server"),
                os.path.join("release", "SS14.Server_MacOS.zip"))


def copy_resources(target, server):
    # Content repo goes FIRST so that it won't override engine files as that's forbidden.
    do_resource_copy(target, "Resources", server)
    do_resource_copy(target, os.path.join("engine", "Resources"), server)


def do_resource_copy(target, base, server):
    for filename in os.listdir(base):
        if filename in SHARED_IGNORED_RESOURCES \
                or filename in (SERVER_IGNORED_RESOURCES if server else CLIENT_IGNORED_RESOURCES):
            continue

        print(filename, base, server)

        path = os.path.join(base, filename)
        target_path = os.path.join(target, filename)
        if os.path.isdir(path):
            os.makedirs(target_path, exist_ok=True)
            _copytree(path, target_path)

        else:
            shutil.copy(path, target_path)


# Hack copied from Stack Overflow to get around the fact that
# shutil.copytree doesn't allow copying into existing directories.
def _copytree(src, dst, symlinks=False, ignore=None):
    for item in os.listdir(src):
        s = os.path.join(src, item)
        d = os.path.join(dst, item)
        if os.path.isdir(s):
            os.makedirs(d, exist_ok=True)
            _copytree(s, d, symlinks, ignore)
        else:
            shutil.copy2(s, d)


def copy_dir_into_zip(directory, zip, basepath):


def package_zip(directory, zipname):
    with zipfile.ZipFile(zipname, "w", zipfile.ZIP_DEFLATED) as zipf:
        for dirs, _, files in os.walk(directory):
            relpath = os.path.relpath(dirs, directory)
            if relpath != ".":
                # Write directory node except for root level.
                zipf.write(dirs, relpath)

            for filename in files:
                zippath = os.path.join(relpath, filename)
                filepath = os.path.join(dirs, filename)

                message = "{dim}{diskroot}{sep}{zipfile}{dim} -> {ziproot}{sep}{zipfile}".format(
                    sep=os.sep + Style.NORMAL,
                    dim=Style.DIM,
                    diskroot=directory,
                    ziproot=zipname,
                    zipfile=os.path.normpath(zippath))

                print(Fore.CYAN + message + Style.RESET_ALL)
                zipf.write(filepath, zippath)


if __name__ == '__main__':
    main()
