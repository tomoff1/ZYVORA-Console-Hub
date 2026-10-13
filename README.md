# ZYVORA — Console Hub

Application Windows native WPF / .NET 8. Licence MIT. Première version 0.1.0 avec un véritable client FTP et des outils de payloads. Pas d'affiliation Sony.

## Utiliser

Installer `dist/ZYVORA-Setup-0.1.0-win-x64.exe`, ou extraire l'archive portable et lancer `ZYVORA.exe`. Aucun runtime .NET séparé nécessaire dans les distributions autonomes.

Saisir l'adresse locale de la console et le port FTP activé. Le port HTTP Payload Manager n'est pas le port FTP. Le firmware déclaré n'est pas une détection automatique. Les identifiants FTP sont anonymes par défaut ; modifier dans Réglages si nécessaire. Le mot de passe n'est pas enregistré.

Les actions qui écrivent sur la console demandent une confirmation explicite. FTP est en clair et limité dans l'application aux adresses IPv4 privées. Ne pas exposer les services au réseau public.

## Compiler et tester

SDK .NET 8, Windows x64 et PowerShell :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
dotnet restore tests/Zyvora.Tests/Zyvora.Tests.csproj --configfile NuGet.Config
dotnet run --project tests/Zyvora.Tests/Zyvora.Tests.csproj -c Release --no-restore
```

Test console en lecture seule (remplacer l'adresse par une IPv4 locale) :
```powershell
dotnet run --project tests/Zyvora.Tests -- --console <IP_LOCALE> 1337
```
Ajouter `--transfer` uniquement pour autoriser le test de création, transfert et suppression dans son dossier temporaire dédié. Le test ne lance aucun payload.

## Organisation

- `src/Zyvora.Core` : protocoles, catalogue, sauvegardes et diagnostics.
- `src/Zyvora.Desktop` : application Windows.
- `src/Zyvora.Setup` : installateur par utilisateur, menu Démarrer et désinstallation.
- `tests` : serveurs de test locaux, contrôles protocolaires et test console optionnel.
- `site` : site statique pour GitHub Pages.
- `catalog` : références d'origine, sans binaires tiers embarqués.
- `ps5` : compagnon ELF prototype, licence GPL-3.0-or-later.
- `docs` : validation et limitations.

Consulter docs/STATUS.md et docs/VALIDATION.md avant distribution. Les fonctions prévues ne sont pas présentées comme disponibles. L'installateur initial n'est pas signé ; sa signature n'a pas été achetée ni configurée.

## Publication

Voir docs/PUBLISHING.md. Les workflows préparent GitHub Pages et GitHub Releases. Aucun dépôt distant n'a été créé automatiquement. Utiliser un compte ou une organisation de projet dont l'identité publique respecte les contraintes de confidentialité.
