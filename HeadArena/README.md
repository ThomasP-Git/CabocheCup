# Head Arena ⚽

Jeu de football arcade original inspiré du principe des jeux de foot à grosses têtes. Moteur, physique, collisions, score et IA en C# ; dessin et clavier dans un client HTML Canvas. Aucun asset externe, aucune dépendance NuGet additionnelle.

## Jouer

Prérequis : SDK .NET 10 (déjà présent sur le Mac utilisé pour créer le jeu).

Sur Mac : double-cliquer sur `Lancer.command` dans le Finder. Si nécessaire, lancer depuis un terminal :

```sh
cd chemin/vers/HeadArena
dotnet run
```

Ouvrir http://127.0.0.1:5187 dans le navigateur. Garder le terminal ouvert pendant la partie. Ctrl+C arrête le serveur. Sur Windows/Linux, utiliser également `dotnet run` puis ouvrir cette adresse.

## Règles et commandes

Le plus de buts après 90 secondes gagne. Un match nul reste nul. Le chronomètre s'arrête pendant les célébrations et les pauses. Le ballon doit passer entièrement sous la barre pour qu'un but soit comptabilisé. Les tirs sont disponibles toutes les 0,32 secondes ; le point vert au-dessus du joueur signale leur disponibilité.

- Bleu : Q/D ou A/D pour bouger, Z ou W pour sauter, Espace pour tirer.
- Orange en duel : flèches gauche/droite, flèche haut pour sauter, Entrée pour tirer.
- P ou Échap : pause / reprise. Le bouton Menu revient à l'accueil.
- Solo : trois niveaux d'IA. Duel : deux joueurs sur le même clavier.
- Boutons tactiles pour le joueur bleu sur écran tactile. Duel conçu pour un clavier.
- Son activable dans la barre sous le terrain.

Chaque onglet dispose de sa propre partie locale. Il ne s'agit pas d'un multijoueur réseau.

## Structure

- `Game.cs` : simulation à 60 Hz, IA et règles.
- `Program.cs` : serveur local ASP.NET Core et WebSocket.
- `wwwroot/index.html` : interface française et rendu Canvas sans bibliothèque.
- `GameTests.cs` : tests du moteur, à lancer avec `dotnet run -- --self-test`.

Graphismes originaux dessinés en code, sans contenu repris du jeu Head Football.
