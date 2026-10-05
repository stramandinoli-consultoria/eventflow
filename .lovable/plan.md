# Ajustar o mapa no celular

## Objetivo
Garantir que o mapa inteiro fique visível e utilizável em telas pequenas, tanto para clientes quanto no editor do evento.

## Alterações
- Tornar a área do mapa responsiva, mantendo as coordenadas originais em uma superfície interna e reduzindo-a proporcionalmente para caber na largura disponível.
- Calcular automaticamente os limites necessários com base nos elementos do evento, evitando cortes laterais e verticais.
- Corrigir o arraste no editor para converter corretamente o movimento do dedo conforme a escala aplicada.
- Ajustar os cabeçalhos e espaçamentos das telas do cliente e do editor para não comprimirem o mapa no celular.
- Validar visualmente as duas telas em viewport móvel e confirmar que os elementos continuam clicáveis e editáveis.

## Detalhes técnicos
O mapa terá uma dimensão lógica estável, enquanto um contêiner responsivo aplica escala uniforme via transformação CSS. O cálculo de posição do arraste levará essa escala em conta.
