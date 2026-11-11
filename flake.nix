{
  description = "MealPrepApp - Cross-platform .NET 10 desktop application (macOS + NixOS)";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachSystem [ "x86_64-linux" ] (system:
      let
        pkgs = import nixpkgs {
          inherit system;
          config.allowUnfree = true;
        };

        dotnet-sdk = with pkgs.dotnetCorePackages; combinePackages [
          sdk_10_0
        ];

        runtimeLibs = with pkgs; [
          fontconfig
          freetype
          libX11
          libICE
          libSM
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
      {
        devShells.default = pkgs.mkShell {
          buildInputs = [ dotnet-sdk ] ++ runtimeLibs;
          LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath runtimeLibs;
          DOTNET_ROOT = "${dotnet-sdk}";
        };

        packages.default = pkgs.buildDotnetModule {
          pname = "mealprep-desktop";
          version = "1.0.0";
          src = pkgs.lib.cleanSourceWith {
            src = ./.;
            filter = path: type:
              pkgs.lib.cleanSourceFilter path type &&
              !(builtins.elem (baseNameOf path) [ "appsettings.Local.json" "bin" "obj" "dist" ".env" ]);
          };
          nugetDeps = ./nix/deps.json;
          projectFile = "App/MealPrepApp.Desktop/MealPrepApp.Desktop.csproj";
          dotnet-sdk = dotnet-sdk;
          dotnet-runtime = pkgs.dotnetCorePackages.runtime_10_0;
          executables = [ "MealPrepApp.Desktop" ];
          runtimeDeps = runtimeLibs;
          postInstall = ''
            install -Dm644 App/MealPrepApp.Desktop/Assets/Icons/app-icon-512.png "$out/share/icons/hicolor/512x512/apps/mealprep.png"
            install -Dm644 App/MealPrepApp.Desktop/Assets/mealprep.desktop "$out/share/applications/mealprep.desktop"
          '';
        };
      }
    );
}
