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
        };
      }
    );
}
