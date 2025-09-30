class Stopwatch:
    def __init__(self):
        self.start_time = None
        self.end_time = None
        self.elapsed_time = 0

    def start(self):
        """Startet die Stoppuhr."""
        self.start_time = time.time()

    def stop(self):
        """Stoppt die Stoppuhr und berechnet die verstrichene Zeit."""
        if self.start_time is None:
            raise ValueError("Die Stoppuhr wurde nicht gestartet.")
        self.end_time = time.time()
        self.elapsed_time = self.end_time - self.start_time

    def get_elapsed_time(self):
        """Gibt die verstrichene Zeit in Sekunden zurück."""
        if self.elapsed_time == 0:
            raise ValueError("Die Stoppuhr wurde noch nicht gestoppt.")
        return self.elapsed_time
