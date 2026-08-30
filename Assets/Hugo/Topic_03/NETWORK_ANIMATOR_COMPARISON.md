# Comparação: NetworkAnimator e locomotion derivada localmente

Esta branch mantém o mesmo Blend Tree do Tema 3, mas altera a origem dos parâmetros usados pelos jogadores remotos para demonstrar o `NetworkAnimator`.

## Fluxo desta versão

1. Somente o owner observa o deslocamento real do próprio `NetworkPlayer`.
2. O `PlayerLocomotionAnimator` do owner calcula e escreve os parâmetros no `Animator`.
3. O `NetworkAnimator`, configurado com autoridade do owner, sincroniza os parâmetros.
4. As outras instâncias não recalculam nem sobrescrevem esses valores; elas reproduzem os valores recebidos.

O movimento continua sendo sincronizado pelo `NetworkTransform`. Não foram criadas `NetworkVariable`, RPCs ou lógica de movimento adicional.

## Animator Parameter Entries

No `NetworkPlayer.prefab`, somente estas entradas estão habilitadas para sincronização:

| Parâmetro | Tipo | Uso |
| --- | --- | --- |
| `Speed` | Float | Idle, Walk e Run |
| `MoveX` | Float | Direção lateral |
| `MoveZ` | Float | Direção longitudinal |

Os controllers masculino e feminino possuem a mesma lista de parâmetros. Caso novos parâmetros sejam adicionados ao controller, eles devem permanecer com `Synchronize` desabilitado até que façam parte de uma demonstração intencional.

## Motivo da comparação

`Speed`, `MoveX` e `MoveZ` são floats suavizados e podem mudar em muitos frames durante a locomoção. Portanto, esta versão pode gerar atualizações frequentes de parâmetros pela rede. Isso é proposital: ela serve para comparar o custo e o comportamento do `NetworkAnimator` com a branch baseada em locomotion derivada localmente, na qual cada instância calcula esses mesmos parâmetros a partir do deslocamento já recebido pelo `NetworkTransform`.

Esta não é uma indicação de que sincronizar os três floats é sempre a melhor solução; é uma implementação acadêmica para tornar a diferença entre as duas abordagens observável.
