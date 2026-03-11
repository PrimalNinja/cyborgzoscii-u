# Protocole UNSIGNAL : Analyse de Renseignement Pas à Pas

**Auteur:** Julian Cassin  
**Date:** 2026-03-11

1. L'Interception
   - Fichier brut obtenu : pas d'en-têtes, pas d'octets magiques, pas de structure
   - Taille du fichier : variable (due au préfixe/suffixe aléatoire)
   - Métadonnées : timing, source, destination — ne mènent nulle part, impossibles à corréler avec le contenu ou l'intention

2. Analyse Statistique (ent)
   - Entropie : 7,99+ bits/octet (maximum)
   - Compression : 0% (entropie parfaite)
   - Khi carré : passe comme aléatoire
   - Corrélation sérielle : proche de zéro
   - Résultat : impossible à distinguer d'un vrai bruit aléatoire

3. Confusion du Système de Coordonnées
   - Adresses d'en-tête : positions absolues dans les 64 premiers Ko de la ROM
   - Adresses de données : relatives au décalage ROM dérivé des indirections H1/H2 elles-mêmes
   - Deux systèmes de coordonnées différents dans le même fichier
   - L'attaquant ne peut pas interpréter les adresses de données sans d'abord :
       a) Reconnaître H1/H2 comme spéciaux (ils ressemblent à des données normales)
       b) Décoder H1/H2 pour obtenir la valeur de décalage
       c) Appliquer le décalage pour réinterpréter toutes les adresses suivantes
   - Probabilité de deviner correctement sans ROM : zéro
   - Même avec la ROM, il faut savoir quelles adresses sont d'en-tête vs. données
   - L'alignement en-tête/données ne se produit que 1/65536 du temps par hasard

4. Analyse du Trafic
   - Préfixe/suffixe aléatoire cache les véritables limites du message
   - H3/H4 sont aussi des indirections elles-mêmes
   - Les décalages de début de ROM variables changent l'interprétation par session
   - Pas de motifs fixes dans les tailles de paquets ou le timing
   - Impossible de déterminer si le fichier contient des données ou est vide

5. Rétro-ingénierie
   - Codeur obtenu : table de correspondance d'une ligne (publique)
   - Algorithme : trivial, la sécurité réside dans la ROM (clé)
   - Savoir comment il fonctionne n'apporte aucun avantage

6. Tentatives de Texte Clair Connu
   - Même texte clair encodé deux fois → sorties différentes
   - Options d'adresses multiples par caractère (sélection aléatoire)
   - Aucun motif reproductible à exploiter

7. Récupération de Clé
   - Force brute : ITS, impossible par définition
   - Canal auxiliaire : simple recherche, pas de mathématiques complexes à fuir
   - La ROM doit être obtenue par des moyens physiques/légaux

8. Le Problème de Vérification
   - N'importe quelle ROM décode quelque chose
   - Mauvaise ROM → déchets (mais des déchets qui ont l'air réels)
   - Pas de sommes de contrôle, pas de MAC, pas d'indicateur de succès
   - Impossible de vérifier quel décodage est "correct"

9. Échelle Combinatoire (exemple d'Autant en emporte le vent)
   - Encodage d'un seul roman : >10^5,500,000 représentations possibles
   - 5 adresses non répétées : 1 billion de combinaisons
   - Suivi jusqu'à épuisement de la mémoire : impossible
   - Aucune collision, jamais

10. Déni Parfait
    - Chaque décodage est intrinsèquement cohérent
    - Toute sortie peut être rejetée comme une coïncidence aléatoire
    - Le décodage "correct" n'est pas défini sans contexte externe

11. Comportement de Compression
    - Les fichiers encodés ZOSCII/UNSIGNAL ne se compressent pas (~0% de taux)
    - La sortie est déjà proche de l'entropie maximale
    - Pour réduire la taille : compresser d'abord l'entrée, puis encoder
    - Le résultat encodé reste incompressible quelle que soit l'entrée

12. L'Authentification et la Détection d'Altération sont Internes
    - MAC si nécessaire : placez-le À L'INTÉRIEUR de la charge utile encodée
    - Sommes de contrôle, signatures, données de vérification : tout va DANS le message
    - Les mêmes règles d'encodage s'appliquent — ils deviennent indiscernables du bruit aléatoire
    - L'attaquant ne peut pas distinguer les données d'authentification du contenu du message
    - Aucun marqueur de validation externe n'existe

13. Conclusion
    - Les outils statistiques renvoient : bruit aléatoire, rien ici
    - L'analyse du trafic est vaincue par le masquage des limites
    - La récupération de clé nécessite la ROM, pas les mathématiques
    - La vérification est impossible même avec un candidat ROM
    - L'authentification est cachée dans la charge utile, impossible à distinguer du message
    - La compression n'est possible qu'avant l'encodage, pas après
    - Le système atteint une clôture épistémique : l'attaquant ne peut pas savoir s'il a gagné
