{ pkgs ? import <nixpkgs> {} }:

let
  dotnet-sdk = with pkgs.dotnetCorePackages; combinePackages [
    sdk_9_0
    (sdk_10_0 or sdk_9_0)
  ];

  runtimeLibs = with pkgs; [
    fontconfig
    freetype
    libX11
    libXcursor
    libXext
    libXi
    libXrandr
    libxkbcommon
    wayland
    libGL
    openssl
    icu
    zlib
  ];
in
pkgs.mkShell {
  name = "mealprep-dev-shell";
  buildInputs = [ dotnet-sdk ] ++ runtimeLibs;
  LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath runtimeLibs;
  DOTNET_ROOT = "${dotnet-sdk}";

  shellHook = ''
    echo "MealPrepApp NixOS development shell ready."
    echo "Run desktop app: dotnet run --project App/MealPrepApp.Desktop"
  '';
}
