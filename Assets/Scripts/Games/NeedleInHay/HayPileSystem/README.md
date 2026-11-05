# Hay Pile System — Unity 6 + URP

Система процедурного стога из обычного prefab/mesh сена.

## 1. Подготовка prefab сена

Создай prefab `HayStraw.prefab`:

- обычный MeshFilter + MeshRenderer;
- любой материал соломы/сена;
- добавь `HayStraw` component;
- Rigidbody НЕ нужен;
- Collider для механики брать сено НЕ нужен.

Важно: `HayStraw` должен быть на корневом объекте prefab.

## 2. Создание стога

Создай Empty:
`HayPile`

Добавь:
`HayPileGenerator`

Перетащи `HayStraw.prefab` в `Straw Prefab`.

Рекомендуемые значения:
- Straw Count: 1800
- Radius: 6
- Height: 4.2
- Surface Bias: 0.35
- Position Jitter: 0.12
- Scale Range: 0.82 / 1.18
- Random Rotation: 180 / 180 / 180

Нажми:
`GENERATE HAY PILE`

Получится объёмный случайный стог с формой купола.

## 3. Игрок

На Player:
- CharacterController
- SimpleFPSController
- HayPileInteractor

В `HayPileInteractor` назначь:
- Player Camera
- Hay Pile
- Hand Anchor
- Inventory Text
- Held Hay Visual

## 4. Взаимодействие

E — взять один кусок.

После взятия:
- кусок удаляется;
- счётчик увеличивается;
- вокруг отверстия несколько соседних кусков
  двигаются внутрь и вниз;
- соседние куски получают небольшой случайный наклон;
- движение плавное, без Rigidbody.

R — положить один кусок (сейчас только инвентарь).

## 5. Максимальное количество

`Max Hay In Hands = 10`.

Поставь любое число: 1, 10, 50 и т.д.

## 6. Настройка осыпания

У HayPileGenerator:

Collapse Radius:
радиус области, которая реагирует.

Collapse Down:
насколько соседние куски проседают.

Collapse Inward:
насколько они съезжают к образовавшейся дырке.

Max Collapsed Per Pick:
сколько кусков реагирует на одно взятие.

Collapse Tilt:
случайный наклон.

Для эффекта как в референсе попробуй:
- Collapse Radius = 1.2
- Collapse Down = 0.10
- Collapse Inward = 0.18
- Max Collapsed Per Pick = 18
- Collapse Tilt = 12

## Производительность

Система не создаёт Rigidbody для каждой соломинки.

Каждая соломинка — обычный MeshRenderer + HayStraw.
При взятии обновляются только ближайшие куски.

Для 1500–3000 кусков это значительно легче полноценной PhysX-кучи.

Если нужно 5000+ кусков, следующий шаг — GPU instancing / Graphics.DrawMeshInstancedIndirect.
