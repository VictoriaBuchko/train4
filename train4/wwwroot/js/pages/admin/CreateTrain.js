document.addEventListener('DOMContentLoaded', function () {
    const carriageTypes = @Html.Raw(Json.Serialize(carriageTypes));
    const carriagesContainer = document.getElementById('carriagesContainer');
    const noCarriages = document.getElementById('noCarriages');
    const addCarriageBtn = document.getElementById('addCarriage');
    const carriageCountInput = document.getElementById('carriageCount');
    const submitBtn = document.getElementById('submitBtn');

    let carriageCounter = 0;

    // Функція для додавання нового вагона
    function addCarriage() {
        carriageCounter++;
        updateCarriageCount();

        const carriageDiv = document.createElement('div');
        carriageDiv.className = 'carriage-item card mb-3';
        carriageDiv.dataset.index = carriageCounter;

        carriageDiv.innerHTML = `
                    <div class="card-header bg-light d-flex justify-content-between align-items-center">
                        <h6 class="mb-0">Вагон #${carriageCounter}</h6>
                        <button type="button" class="btn btn-outline-danger btn-sm remove-carriage">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                    <div class="card-body">
                        <div class="row">
                            <div class="col-md-6 mb-3">
                                <label class="form-label">Тип вагона</label>
                                <select name="carriageTypeIds" class="form-select carriage-type-select" required>
                                    <option value="" disabled selected>Оберіть тип вагона</option>
                                    ${carriageTypes.map(ct => `<option value="${ct.carriage_type_id}">${ct.carriage_type} (${ct.seat_count} місць)</option>`).join('')}
                                </select>
                            </div>
                            <div class="col-md-6 mb-3">
                                <label class="form-label">Номер вагона в составі</label>
                                <input type="number" name="carriageNumbers" class="form-control" value="${carriageCounter}" min="1" required>
                            </div>
                        </div>
                    </div>
                `;

        carriagesContainer.appendChild(carriageDiv);

        // Додаємо обробник для видалення вагона
        carriageDiv.querySelector('.remove-carriage').addEventListener('click', function () {
            carriageDiv.remove();
            updateCarriageCount();
            updateCarriageNumbers();
        });

        updateVisibility();
    }

    // Функція для оновлення лічильника вагонів
    function updateCarriageCount() {
        const carriageItems = document.querySelectorAll('.carriage-item');
        carriageCountInput.value = carriageItems.length;
        submitBtn.disabled = carriageItems.length === 0;
    }

    // Функція для перенумерації вагонів після видалення
    function updateCarriageNumbers() {
        const carriageItems = document.querySelectorAll('.carriage-item');
        carriageItems.forEach((item, index) => {
            const header = item.querySelector('h6');
            header.textContent = `Вагон #${index + 1}`;
        });
    }

    // Функція для оновлення видимості елементів
    function updateVisibility() {
        if (carriageCounter > 0) {
            noCarriages.style.display = 'none';
        } else {
            noCarriages.style.display = 'block';
        }
    }

    // Додаємо обробник для кнопки додавання вагона
    addCarriageBtn.addEventListener('click', addCarriage);

    // Перевірка форми перед відправкою
    document.getElementById('createTrainForm').addEventListener('submit', function (e) {
        const carriageItems = document.querySelectorAll('.carriage-item');
        if (carriageItems.length === 0) {
            e.preventDefault();
            alert('Додайте хоча б один вагон до потяга!');
        }

        // Перевірка вибору типів вагонів
        let allTypesSelected = true;
        document.querySelectorAll('.carriage-type-select').forEach(select => {
            if (!select.value) {
                allTypesSelected = false;
            }
        });

        if (!allTypesSelected) {
            e.preventDefault();
            alert('Оберіть тип для кожного вагона!');
        }
    });

    // Додаємо перший вагон автоматично при завантаженні сторінки
    addCarriage();
});