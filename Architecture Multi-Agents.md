# Carte mentale complète du système multi-agents

## Vue d'ensemble

```text
UTILISATEUR
↓
PLANNER
↓
[pour chaque étape]
  SPECIALIST
  ↓
  SUPERVISOR (pre)
  ↓
  CRITIC
  ↓
  EXECUTOR
  ↓
  SUPERVISOR (post)
↓
SYNTHESIZER
↓
UTILISATEUR
```

---

# Carte mentale détaillée

```text
SYSTÈME MULTI-AGENTS
│
├── 1. PLANNER
│   │
│   ├── Rôle
│   │   ├── construire le plan global
│   │   ├── choisir les outils
│   │   └── ordonner les étapes
│   │
│   ├── Entrées
│   │   ├── prompt utilisateur
│   │   ├── description des outils
│   │   └── éventuellement mémoire / contexte
│   │
│   ├── Actions
│   │   ├── comprendre l’intention
│   │   ├── détecter si la demande est mono ou multi-étapes
│   │   ├── produire un plan complet
│   │   └── respecter l’ordre explicite du prompt ("ensuite", "puis", "après")
│   │
│   ├── Sortie attendue
│   │   └── JSON
│   │       {
│   │         "plan": [
│   │           { "tool": "...", "parameters": {} }
│   │         ],
│   │         "why": "raison courte"
│   │       }
│   │
│   ├── Ne doit pas
│   │   ├── rédiger la réponse finale
│   │   ├── exécuter les outils
│   │   └── retourner un plan partiel si la demande est explicitement multi-étapes
│   │
│   └── Attendu
│       ├── plan cohérent
│       ├── ordre correct
│       └── tous les outils nécessaires
│
├── 2. SPECIALIST
│   │
│   ├── Rôle
│   │   ├── préparer l’appel d’outil concret
│   │   └── traduire le step du planner en paramètres exploitables
│   │
│   ├── Entrées
│   │   ├── nom de l’outil
│   │   ├── paramètres du plan
│   │   ├── prompt spécialisé de l’outil
│   │   └── résultats des étapes précédentes
│   │
│   ├── Actions
│   │   ├── corriger une requête SQL
│   │   ├── compléter un specJson Excel
│   │   ├── injecter les données issues d’une étape précédente
│   │   └── adapter le format exact attendu par l’outil
│   │
│   ├── Sortie attendue
│   │   └── JSON
│   │       {
│   │         "tool": "...",
│   │         "parameters": { ... }
│   │       }
│   │
│   ├── Ne doit pas
│   │   ├── changer l’ordre global du plan
│   │   ├── inventer une stratégie alternative
│   │   └── agir comme planner bis
│   │
│   └── Attendu
│       ├── appel outil propre
│       ├── paramètres complets
│       └── compatibilité réelle avec l’outil
│
├── 3. SUPERVISOR (PRE-EXECUTE)
│   │
│   ├── Rôle
│   │   ├── faire un contrôle avant exécution
│   │   └── bloquer les incohérences évidentes
│   │
│   ├── Entrées
│   │   ├── tool call proposé
│   │   ├── paramètres
│   │   ├── étapes déjà exécutées
│   │   └── contexte courant
│   │
│   ├── Actions
│   │   ├── vérifier la présence des paramètres minimaux
│   │   ├── vérifier qu’une dépendance existe déjà si elle est requise
│   │   ├── autoriser ou bloquer l’exécution
│   │   └── idéalement le faire de manière déterministe pour les cas simples
│   │
│   ├── Décisions possibles
│   │   ├── continue
│   │   ├── replan
│   │   └── stop
│   │
│   ├── Ne doit pas
│   │   ├── retourner un plan
│   │   ├── réordonner les étapes
│   │   ├── juger un échec technique avant exécution
│   │   └── raconter ce que “le workflow devrait être”
│   │
│   └── Attendu
│       ├── contrôle sobre
│       ├── décision courte
│       └── protection contre les incohérences grossières
│
├── 4. CRITIC
│   │
│   ├── Rôle
│   │   ├── relire l’appel outil avant exécution
│   │   └── détecter un mauvais paramétrage ou une incohérence locale
│   │
│   ├── Entrées
│   │   ├── tool call proposé
│   │   ├── prompt utilisateur
│   │   ├── step courant
│   │   └── éventuellement le plan global pour contexte
│   │
│   ├── Actions
│   │   ├── approuver l’appel
│   │   ├── demander une correction de paramètres
│   │   ├── refuser une étape dangereuse ou absurde
│   │   └── signaler un problème global seulement s’il est réellement bloquant
│   │
│   ├── Décisions possibles
│   │   ├── approve
│   │   ├── retry
│   │   ├── replan
│   │   └── reject
│   │
│   ├── Sortie attendue
│   │   └── JSON
│   │       {
│   │         "decision": "...",
│   │         "why": "...",
│   │         "revisedParameters": { ... }
│   │       }
│   │
│   ├── Ne doit pas
│   │   ├── retourner un plan
│   │   ├── devenir planner
│   │   ├── juger la mission globale au lieu du step courant
│   │   └── demander un replan juste parce qu’il reste d’autres étapes à venir
│   │
│   └── Attendu
│       ├── validation locale
│       ├── critique utile
│       └── sortie strictement conforme au contrat
│
├── 5. EXECUTOR
│   │
│   ├── Rôle
│   │   ├── exécuter réellement l’outil
│   │   └── produire un résultat concret
│   │
│   ├── Type
│   │   └── code pur (pas un LLM)
│   │
│   ├── Entrées
│   │   ├── nom de l’outil
│   │   └── paramètres finaux
│   │
│   ├── Actions
│   │   ├── lancer la requête SQL
│   │   ├── générer un fichier
│   │   ├── envoyer un mail
│   │   ├── appeler une API
│   │   └── retourner le résultat brut
│   │
│   ├── Sortie attendue
│   │   └── résultat structuré
│   │       {
│   │         "ok": true,
│   │         ...
│   │       }
│   │
│   ├── Ne doit pas
│   │   ├── interpréter la stratégie
│   │   ├── corriger la mission
│   │   └── inventer des paramètres
│   │
│   └── Attendu
│       ├── exécution fiable
│       ├── résultat traçable
│       └── comportement déterministe
│
├── 6. SUPERVISOR (POST-EXECUTE)
│   │
│   ├── Rôle
│   │   ├── vérifier le résultat réel après exécution
│   │   └── décider si on continue, replannifie ou stoppe
│   │
│   ├── Entrées
│   │   ├── résultat de l’outil
│   │   ├── step courant
│   │   └── historique des étapes exécutées
│   │
│   ├── Actions
│   │   ├── vérifier si le résultat est exploitable
│   │   ├── détecter un échec technique
│   │   ├── détecter un résultat vide ou inutilisable
│   │   └── laisser passer vers l’étape suivante si tout va bien
│   │
│   ├── Décisions possibles
│   │   ├── continue
│   │   ├── replan
│   │   └── stop
│   │
│   ├── Ne doit pas
│   │   ├── inventer un résultat non présent
│   │   ├── réécrire le plan complet
│   │   └── faire de la narration
│   │
│   └── Attendu
│       ├── validation factuelle
│       ├── décision fondée sur le résultat réel
│       └── passage propre au step suivant
│
└── 7. SYNTHESIZER
    │
    ├── Rôle
    │   ├── formuler la réponse finale utilisateur
    │   └── résumer ce qui a réellement été fait
    │
    ├── Entrées
    │   ├── prompt utilisateur
    │   └── liste des résultats réels des outils exécutés
    │
    ├── Actions
    │   ├── agréger les résultats
    │   ├── rendre la réponse lisible
    │   ├── expliquer ce qui a été réalisé
    │   └── signaler ce qui n’a pas été fait si le workflow s’est interrompu
    │
    ├── Ne doit pas
    │   ├── halluciner des actions non exécutées
    │   ├── dire qu’un mail a été envoyé sans trace réelle
    │   ├── dire qu’un fichier a été créé sans filePath réel
    │   └── répondre comme si le plan complet avait réussi si ce n’est pas le cas
    │
    └── Attendu
        ├── réponse fidèle aux ToolResults
        ├── aucune invention
        └── formulation claire pour l’utilisateur
```

---

# Ordre d’appel réel

## Pipeline nominal

```text
1. Utilisateur
2. Planner
3. Pour chaque étape :
   3.1 Specialist
   3.2 Supervisor (pre)
   3.3 Critic
   3.4 Executor
   3.5 Supervisor (post)
4. Synthesizer
5. Utilisateur
```

---

# Résumé par agent

## Planner
Décide **quoi faire** et **dans quel ordre**.

## Specialist
Prépare **comment appeler l’outil**.

## Supervisor (pre)
Vérifie **si on peut lancer l’étape maintenant**.

## Critic
Vérifie **si l’appel outil est sain**.

## Executor
Fait **l’action réelle**.

## Supervisor (post)
Vérifie **si le résultat obtenu est exploitable**.

## Synthesizer
Explique **ce qui a réellement été fait**.

---

# Distinction essentielle

```text
Planner     = stratégie
Specialist  = préparation
Critic      = contrôle qualité
Supervisor  = garde-fou
Executor    = action réelle
Synthesizer = formulation finale
```

---

# Point d’attention architectural

Les rôles les plus susceptibles de dériver sont :

- Planner
- Critic
- Supervisor

Pourquoi ?

Parce que ce sont eux qui “raisonnent”.

Les rôles les plus mécaniques sont :

- Executor
- partiellement Specialist
- Synthesizer si on le bride bien

Donc, en production, la robustesse vient souvent de :
- réduire le champ d’action cognitif du Critic
- rendre le Supervisor pré plus déterministe
- empêcher le Synthesizer d’inventer
- laisser le Planner être le seul vrai décideur de l’ordre global
