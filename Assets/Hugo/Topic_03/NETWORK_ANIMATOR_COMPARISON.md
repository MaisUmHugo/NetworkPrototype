# Demonstração: animação não sincronizada

A branch `Exercise_03_NoAnimationSync` é propositalmente incompleta. Ela demonstra por que sincronizar somente o Transform não é suficiente para reproduzir visualmente a locomotion de um jogador remoto.

## Fluxo desta versão

1. O owner movimenta seu `NetworkPlayer`.
2. O `NetworkTransform` sincroniza posição e rotação.
3. O `PlayerLocomotionAnimator` do owner calcula `Speed`, `MoveX` e `MoveZ`.
4. O owner reproduz Idle, Walk e Run normalmente.
5. O jogador remoto recebe o movimento, mas não recebe nem deriva os parâmetros do `Animator`.
6. O remoto permanece em Idle enquanto se desloca pelo cenário.

O `NetworkAnimator` foi removido do `NetworkPlayer.prefab`. Não foram adicionadas `NetworkVariable`, RPCs, mensagens customizadas ou qualquer substituto para sincronizar animação.

## Comparação acadêmica

| Branch | Transform | Origem da animação remota |
| --- | --- | --- |
| `BlendTree` | `NetworkTransform` | Derivada localmente do deslocamento recebido |
| `Exercise_03_NetworkAnimator` | `NetworkTransform` | Parâmetros enviados pelo `NetworkAnimator` |
| `Exercise_03_NoAnimationSync` | `NetworkTransform` | Nenhuma; o remoto permanece em Idle |

Os mesmos controllers, Blend Trees e parâmetros `Speed`, `MoveX` e `MoveZ` foram preservados para manter uma comparação justa.

## Finalidade

Esta versão existe exclusivamente para a gravação acadêmica “Por que sincronizar animações?”. O deslizamento do personagem remoto em Idle é o resultado esperado, não um comportamento recomendado para produção.
