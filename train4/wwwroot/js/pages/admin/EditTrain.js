document.addEventListener('DOMContentLoaded', function () {
    const ctypes = @Html.Raw(Json.Serialize(carriageTypes));
    const ecarriages = @Html.Raw(Json.Serialize(existingCarriages));
    const bookings = @Html.Raw(Json.Serialize(carriagesWithBookings ?? new List < Dictionary < string, object >> ()));
    const carriagesContainer = document.getElementById('carriagesContainer');
    const noCarriages = document.getElementById('noCarriages');
    const addCarriageBtn = document.getElementById('addCarriage');
    const carriageCountInput = document.getElementById('carriageCount');
    const submitBtn = document.getElementById('submitBtn');

    let carriageCounter = 0;
    let maxCarriageNumber = 0;

    // ВИПРАВЛЕНА функція для перевірки чи КОНКРЕТНИЙ вагон має бронювання
    function carriageHasBookings(carriageId) {
        if (!bookings || !carriageId) return false;
        return bookings.some(c => parseInt(c.train_carriage_types_id) === parseInt(carriageId));
    }

    // Функція для створення HTML вагона
    function createCarriageHtml(carriage, isExisting = false) {
        const carriageNumber = isExisting ? carriage.carriage_number : maxCarriageNumber + 1;
        const selectedTypeId = isExisting ? carriage.carriage_type_id : '';
        const carriageId = isExisting ? carriage.train_carriage_types_id : 0;

        // ВИПРАВЛЕНО: перевіряємо тільки цей конкретний вагон
        const hasBookings = isExisting && carriageHasBookings(carriageId);

        const hasBookingsClass = hasBookings ? 'border-danger' : '';
        const hasBookingsText = hasBookings ?
            '<span class="badge bg-danger ms-2">Є бронювання</span>' : '';

        console.log(`Вагон ${carriageNumber}: ID=${carriageId}, hasBookings=${hasBookings}`); // Для відладки

        return `
                    <div class="carriage-item card mb-3 ${hasBookingsClass}" data-index="${carriageCounter}" 
                         data-existing="${isExisting}" data-carriage-id="${carriageId}">
                        <div class="card-header ${hasBookings ? 'bg-danger-subtle' : 'bg-light'} d-flex justify-content-between align-items-center">
                            <h6 class="mb-0">
                                Вагон #${carriageNumber}
                                ${hasBookingsText}
                            </h6>
                            <button type="button" class="btn btn-outline-danger btn-sm remove-carriage" 
                                    ${hasBookings ? 'disabled title="Неможливо видалити - є активні бронювання"' : ''}>
                                <i class="bi bi-trash"></i>
                            </button>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label class="form-label">Тип вагона</label>
                                    <select name="carriageTypeIds" class="form-select carriage-type-select" required
                                            ${hasBookings ? 'disabled title="Неможливо змінити тип - є активні бронювання"' : ''}>
                                        <option value="" disabled ${!isExisting ? 'selected' : ''}>Оберіть тип вагона</option>
                                        ${ctypes.map(ct =>
            `<option value="${ct.carriage_type_id}" ${selectedTypeId == ct.carriage_type_id ? 'selected' : ''}>
                                                ${ct.carriage_type} (${ct.seat_count} місць)
                                            </option>`
        ).join('')}
                                    </select>
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label class="form-label">Номер вагона в составі</label>
                                    <input type="number" name="carriageNumbers" class="form-control" 
                                           value="${carriageNumber}" min="1" required
                                           ${hasBookings ? 'readonly title="Неможливо змінити номер - є активні бронювання"' : ''}>
                                    <!-- Приховане поле для ID існуючого вагона -->
                                    <input type="hidden" name="existingCarriageIds" value="${carriageId}">
                                </div>
                            </div>
                            ${hasBookings ?
                '<div class="alert alert-warning alert-sm mb-0"><small><i class="bi bi-exclamation-triangle"></i> Цей вагон має активні бронювання і не може бути змінений або видалений</small></div>' :
                '<div class="alert alert-info alert-sm mb-0"><small><i class="bi bi-info-circle"></i> Цей вагон можна вільно редагувати</small></div>'}
                        </div>
                    </div>
                `;
    }

    // Функція для додавання нового вагона
    function addCarriage(existingCarriage = null) {
        carriageCounter++;

        if (!existingCarriage) {
            maxCarriageNumber = Math.max(maxCarriageNumber + 1, carriageCounter);
        }

        const carriageDiv = document.createElement('div');
        carriageDiv.innerHTML = createCarriageHtml(existingCarriage, !!existingCarriage);
        const carriageElement = carriageDiv.firstElementChild;
        carriagesContainer.appendChild(carriageElement);

        // Додаємо обробник для видалення вагона
        const removeBtn = carriageElement.querySelector('.remove-carriage');
        if (removeBtn && !removeBtn.disabled) {
            removeBtn.addEventListener('click', function () {
                const carriageNum = carriageElement.querySelector('h6').textContent.match(/\d+/)[0];
                if (confirm(`Ви впевнені, що хочете видалити вагон #${carriageNum}?`)) {
                    carriageElement.remove();
                    updateCarriageCount();
                    updateCarriageNumbers();
                }
            });
        }

        updateCarriageCount();
        updateVisibility();
    }

    // Завантажуємо існуючі вагони
    if (ecarriages && ecarriages.length > 0) {
        console.log('Завантаження існуючих вагонів:', ecarriages);
        console.log('Вагони з бронюваннями:', bookings);

        ecarriages.forEach(carriage => {
            maxCarriageNumber = Math.max(maxCarriageNumber, carriage.carriage_number);
            addCarriage(carriage);
        });
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
            const isExisting = item.dataset.existing === 'true';
            const hasBookings = item.classList.contains('border-danger');
            const bookingsBadge = hasBookings ? '<span class="badge bg-danger ms-2">Є бронювання</span>' : '';

            if (!isExisting && !hasBookings) {
                const currentNumber = index + 1;
                header.innerHTML = `Вагон #${currentNumber}${bookingsBadge}`;
                const numberInput = item.querySelector('input[name="carriageNumbers"]');
                if (!numberInput.readOnly) {
                    numberInput.value = currentNumber;
                }
            }
        });
    }

    // Функція для оновлення видимості елементів
    function updateVisibility() {
        const carriageItems = document.querySelectorAll('.carriage-item');
        if (carriageItems.length === 0) {
            noCarriages.style.display = 'block';
        } else {
            noCarriages.style.display = 'none';
        }
    }

    // Додаємо обробник для кнопки додавання вагона
    addCarriageBtn.addEventListener('click', () => addCarriage());

    // ВИПРАВЛЕНА перевірка форми перед відправкою
    document.getElementById('editTrainForm').addEventListener('submit', function (e) {
        const carriageItems = document.querySelectorAll('.carriage-item');
        if (carriageItems.length === 0) {
            e.preventDefault();
            alert('Потяг має мати хоча б один вагон!');
            return;
        }

        // Перевірка вибору типів вагонів (тільки для НЕ заблокованих)
        let allTypesSelected = true;
        document.querySelectorAll('.carriage-type-select').forEach(select => {
            if (!select.value && !select.disabled) {
                allTypesSelected = false;
            }
        });

        if (!allTypesSelected) {
            e.preventDefault();
            alert('Оберіть тип для кожного вагона!');
            return;
        }

        // Перевірка унікальності номерів вагонів
        const carriageNumbers = [];
        document.querySelectorAll('input[name="carriageNumbers"]').forEach(input => {
            carriageNumbers.push(parseInt(input.value));
        });

        const uniqueNumbers = [...new Set(carriageNumbers)];
        if (carriageNumbers.length !== uniqueNumbers.length) {
            e.preventDefault();
            alert('Номери вагонів повинні бути унікальними!');
            return;
        }

        // Підтвердження збереження
        const hasBookingsCount = document.querySelectorAll('.border-danger').length;
        const editableCount = carriageItems.length - hasBookingsCount;

        if (hasBookingsCount > 0) {
            const confirmMsg = `Буде збережено зміни для ${editableCount} вагонів. ${hasBookingsCount} вагонів з бронюваннями залишаться без змін. Продовжити?`;
            if (!confirm(confirmMsg)) {
                e.preventDefault();
                return;
            }
        }
    });

    // Ініціалізація
    updateCarriageCount();
    updateVisibility();
});