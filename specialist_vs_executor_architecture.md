# Specialist vs Executor dans une architecture multi‑agents

## Vue rapide

  -----------------------------------------------------------------------
  Agent                   Type                    Rôle
  ----------------------- ----------------------- -----------------------
  Planner                 LLM                     Construit le plan
                                                  global d'actions

  Specialist              LLM                     Prépare et corrige
                                                  l'appel d'outil

  Executor                Code                    Exécute réellement
                                                  l'outil

  Critic                  LLM                     Valide / corrige
                                                  l'appel avant exécution

  Supervisor              Règles / LLM            Vérifie la cohérence
                                                  avant et après
                                                  exécution

  Synthesizer             LLM                     Produit la réponse
                                                  finale utilisateur
  -----------------------------------------------------------------------

------------------------------------------------------------------------

# 1. Specialist

Le **Specialist** est encore un agent LLM.

Son rôle est de **préparer correctement l'appel d'un outil** à partir du
plan.

Il agit comme un **traducteur intelligent entre le plan et l'outil
réel**.

## Entrée

``` json
{
  "tool": "sql_query",
  "parameters": {
    "bddname": "Temporis",
    "query": "..."
  }
}
```

-   prompt spécialisé de l'outil\
-   contexte éventuel (résultats précédents)

## Ce qu'il peut faire

-   corriger une requête SQL
-   compléter des paramètres
-   adapter un format JSON
-   injecter un résultat précédent
-   générer un `specJson` correct pour Excel

### Exemple

Planner propose :

``` json
sql_query
query = SELECT NOM, PRENOM FROM conges ...
bddname = HR_DB
```

Le specialist corrige :

``` json
sql_query
query = SELECT wf.LAST_NAME ...
bddname = Temporis
```

Il **améliore donc le plan avant exécution**.

## Sortie

``` json
{
  "tool": "sql_query",
  "parameters": {
    "query": "...",
    "bddname": "Temporis"
  }
}
```

------------------------------------------------------------------------

# 2. Executor

L'**Executor n'est pas un LLM**.

C'est du **code pur**.

Il exécute réellement l'outil demandé.

### Exemple

``` csharp
var result = await tool.ExecuteAsync(parameters);
```

Dans ton système :

    SqlQueryTool.ExecuteAsync()

## Entrée

``` json
{
  "tool": "sql_query",
  "parameters": {...}
}
```

## Action

Exécuter la requête SQL ou l'outil demandé.

## Sortie

``` json
{
  "ok": true,
  "rows": [...]
}
```

------------------------------------------------------------------------

# 3. Comparaison simple

  Agent        Type           Ce qu'il fait
  ------------ -------------- -----------------------
  Planner      LLM            construit le plan
  Specialist   LLM            prépare l'appel outil
  Executor     Code           exécute l'outil
  Critic       LLM            valide l'appel
  Supervisor   Règles / LLM   contrôle la cohérence

------------------------------------------------------------------------

# 4. Pourquoi séparer Specialist et Executor

Les outils réels sont **très stricts**.

Exemple :

Un outil Excel peut attendre :

``` json
specJson
{
  sheets: [...]
}
```

Mais le planner peut produire :

``` json
columns: [...]
data: [...]
```

Le **Specialist** peut convertir ce format avant l'exécution.

Sans lui, il faut :

-   soit un planner parfait
-   soit beaucoup de code de normalisation

------------------------------------------------------------------------

# 5. Architecture typique simplifiée

Dans beaucoup de systèmes de production :

    Planner
    ↓
    Executor
    ↓
    Synthesizer

Le Specialist est parfois supprimé pour :

-   réduire les appels LLM
-   réduire la latence
-   simplifier l'architecture

------------------------------------------------------------------------

# 6. Architecture actuelle de ton système

    Planner
    ↓
    Specialist
    ↓
    Supervisor (pre)
    ↓
    Critic
    ↓
    Executor
    ↓
    Supervisor (post)
    ↓
    Synthesizer

Cela signifie **plusieurs décisions cognitives avant l'exécution
réelle**.

C'est puissant mais peut introduire :

-   de la latence
-   des comportements instables
-   des replans inutiles

------------------------------------------------------------------------

# 7. Résumé final

    Specialist = prépare l'appel outil (LLM)
    Executor   = exécute l'outil (code)

Les deux rôles sont donc **complémentaires mais différents**.
